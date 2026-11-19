using System.Runtime.InteropServices;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;

namespace Olve.Engine3D.Samples.Synth.Rendering;

/// <summary>A batch that can keep instances in retained layers (<see cref="BatchLayers"/>).</summary>
public interface ILayeredBatch
{
    /// <summary>Clears the layer, and adds into it until <see cref="IntoFrame"/>.</summary>
    void Into(int layer);

    /// <summary>Adds into the frame again.</summary>
    void IntoFrame();

    /// <summary>How many instances the frame has so far.</summary>
    int FrameCount { get; }

    /// <summary>The frame's instances from <paramref name="from"/> on, kept for <see cref="Replay"/>.</summary>
    Array CopyFrame(int from);

    /// <summary>Adds instances <see cref="CopyFrame"/> kept to the frame again.</summary>
    void Replay(Array instances);
}

/// <summary>Where a batch's instances are shown: slots in a group, each added once and then updated in place.</summary>
public interface IInstanceSlots<TInstance>
    where TInstance : unmanaged, IInstanceData
{
    Result<InstanceId<TInstance>> Add(TInstance instance);

    Result Update(InstanceId<TInstance> id, TInstance instance);
}

/// <summary>A group's instances as a batch's slots.</summary>
public sealed class GroupSlots<TVertex, TInstance>(GroupManager groups, GroupId<TVertex, TInstance> group) : IInstanceSlots<TInstance>
    where TVertex : IVertexData
    where TInstance : unmanaged, IInstanceData
{
    public Result<InstanceId<TInstance>> Add(TInstance instance) => groups.AddInstance(group, instance);

    public Result Update(InstanceId<TInstance> id, TInstance instance) => groups.UpdateInstance(group, id, instance);
}

/// <summary>
/// One group's instances. What stays put is drawn once into a retained layer (<see cref="BatchLayers.Draw"/>) and kept;
/// the rest is added again each frame. The layers come first, in order, then the frame's, so in a group drawn in order
/// (panel ink) a frame's shape lies over a layer's. <see cref="End"/> updates only the instances that changed, and looks
/// at a layer's only after it was drawn again. Slots left over from a busier frame get <paramref name="hidden"/>, an
/// instance that draws nothing.
/// </summary>
public sealed class InstanceBatch<TVertex, TInstance>(IInstanceSlots<TInstance> slots, TInstance hidden) : ILayeredBatch
    where TVertex : IVertexData
    where TInstance : unmanaged, IInstanceData
{
    public InstanceBatch(GroupManager groups, GroupId<TVertex, TInstance> group, TInstance hidden)
        : this(new GroupSlots<TVertex, TInstance>(groups, group), hidden)
    {
    }

    private readonly List<List<TInstance>> _layers = [];
    private readonly List<TInstance> _frame = [];
    private readonly List<InstanceId<TInstance>> _ids = [];
    // What each slot shows, as uploaded
    private TInstance[] _shown = [];
    private List<TInstance>? _target;
    private int _visible;
    // The first layer drawn since the last End, if any
    private int _changedLayer = int.MaxValue;


    public void Begin() => _frame.Clear();

    public void Add(TInstance instance) => (_target ?? _frame).Add(instance);

    public void Into(int layer)
    {
        while (_layers.Count <= layer)
        {
            _layers.Add([]);
        }

        _target = _layers[layer];
        _target.Clear();
        _changedLayer = int.Min(_changedLayer, layer);
    }

    public void IntoFrame() => _target = null;

    public int FrameCount => _frame.Count;

    public Array CopyFrame(int from) => CollectionsMarshal.AsSpan(_frame)[from..].ToArray();

    public void Replay(Array instances) => _frame.AddRange((TInstance[])instances);

    public Result End()
    {
        // Slots before the first layer drawn again hold what they held
        var slot = 0;
        for (var layer = 0; layer < _layers.Count && layer < _changedLayer; layer++)
        {
            slot += _layers[layer].Count;
        }

        for (var layer = _changedLayer; layer < _layers.Count; layer++)
        {
            if (Show(slot, CollectionsMarshal.AsSpan(_layers[layer])).TryPickProblems(out var problems))
            {
                return problems;
            }

            slot += _layers[layer].Count;
        }

        if (Show(slot, CollectionsMarshal.AsSpan(_frame)).TryPickProblems(out var frameProblems))
        {
            return frameProblems;
        }

        slot += _frame.Count;
        for (var i = slot; i < _visible; i++)
        {
            if (Set(i, hidden).TryPickProblems(out var problems))
            {
                return problems;
            }
        }

        _visible = slot;
        _changedLayer = int.MaxValue;
        return Result.Success();
    }

    // Shows the instances from a slot on. They are compared as bytes, in one go first, as most are as they were: a
    // field-by-field comparison is slow in the browser's interpreter.
    private Result Show(int start, ReadOnlySpan<TInstance> instances)
    {
        var known = int.Clamp(_ids.Count - start, 0, instances.Length);
        if (known > 0 && MemoryMarshal.AsBytes(instances[..known]).SequenceEqual(MemoryMarshal.AsBytes(_shown.AsSpan(start, known))))
        {
            instances = instances[known..];
            start += known;
        }

        for (var i = 0; i < instances.Length; i++)
        {
            if (Set(start + i, instances[i]).TryPickProblems(out var problems))
            {
                return problems;
            }
        }

        return Result.Success();
    }

    private Result Set(int slot, TInstance data)
    {
        if (slot >= _ids.Count)
        {
            if (slots.Add(data).TryPickProblems(out var problems, out var id))
            {
                return problems;
            }

            _ids.Add(id);
            if (_shown.Length < _ids.Count)
            {
                Array.Resize(ref _shown, int.Max(16, _shown.Length * 2));
            }

            _shown[slot] = data;
            return Result.Success();
        }

        if (MemoryMarshal.AsBytes(new ReadOnlySpan<TInstance>(in data)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<TInstance>(in _shown[slot]))))
        {
            return Result.Success();
        }

        if (slots.Update(_ids[slot], data).TryPickProblems(out var updateProblems))
        {
            return updateProblems;
        }

        _shown[slot] = data;
        return Result.Success();
    }
}

/// <summary>
/// The batches whose instances can be kept in retained layers: <see cref="Draw"/> draws a layer again, the batches
/// adding into it, and the frame's drawing adds into the frame as before. A batch made while a layer is drawn joins it.
/// What is drawn each frame but seldom changes can be kept too, in a <see cref="Memo{TKey}"/>: <see cref="Draw{TKey, TState}"/>
/// adds what it drew last time again while its key, what the drawing reads, is the same.
/// </summary>
public sealed class BatchLayers
{
    private readonly List<ILayeredBatch> _batches = [];
    private int? _drawing;
    private int[] _counts = [];

    public T Add<T>(T batch) where T : ILayeredBatch
    {
        _batches.Add(batch);
        if (_drawing is { } layer)
        {
            batch.Into(layer);
        }

        return batch;
    }

    /// <summary>Clears the layer in every batch and draws it again.</summary>
    public Result Draw(int layer, Func<Result> draw)
    {
        _drawing = layer;
        foreach (var batch in _batches)
        {
            batch.Into(layer);
        }

        try
        {
            return draw();
        }
        finally
        {
            _drawing = null;
            for (var i = 0; i < _batches.Count; i++)
            {
                _batches[i].IntoFrame();
            }
        }
    }

    /// <summary>Into the frame: what <paramref name="draw"/> drew for the memo last time if the key is the same, else draws it again and keeps it.</summary>
    public void Draw<TKey, TState>(Memo<TKey> memo, TKey key, TState state, Action<TState> draw)
    {
        if (memo.Drawn && EqualityComparer<TKey>.Default.Equals(memo.Key, key))
        {
            foreach (var (batch, instances) in memo.Instances)
            {
                batch.Replay(instances);
            }

            return;
        }

        if (_counts.Length < _batches.Count)
        {
            _counts = new int[_batches.Count * 2];
        }

        for (var i = 0; i < _batches.Count; i++)
        {
            _counts[i] = _batches[i].FrameCount;
        }

        var before = _batches.Count;
        draw(state);
        memo.Instances.Clear();
        for (var i = 0; i < _batches.Count; i++)
        {
            var from = i < before ? _counts[i] : 0;
            if (_batches[i].FrameCount > from)
            {
                memo.Instances.Add((_batches[i], _batches[i].CopyFrame(from)));
            }
        }

        memo.Key = key;
        memo.Drawn = true;
    }
}

/// <summary>What one part drew into the frame last time (<see cref="BatchLayers.Draw{TKey, TState}"/>), and the key it drew it for.</summary>
public sealed class Memo<TKey>
{
    /// <summary>The memo of each of a kind of part, by the part's own reference.</summary>
    public static Memo<TKey> Of<TPart>(Dictionary<TPart, Memo<TKey>> memos, TPart part) where TPart : notnull =>
        memos.TryGetValue(part, out var memo) ? memo : memos[part] = new Memo<TKey>();

    internal TKey Key { get; set; } = default!;
    internal bool Drawn { get; set; }
    internal List<(ILayeredBatch Batch, Array Instances)> Instances { get; } = [];
}
