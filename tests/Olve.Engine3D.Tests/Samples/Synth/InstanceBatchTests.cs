using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Samples.Synth.Rendering;
using Olve.Results;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>Which slots of its group a batch fills with what, over frames, retained layers and memos.</summary>
public class InstanceBatchTests
{
    private const int Hidden = -1;

    private readonly BatchLayers _layers = new();

    [Test]
    public async Task A_shrunken_layer_and_a_grown_frame_reuse_the_slots_and_only_changed_ones_are_sent()
    {
        var (batch, slots) = Batch();
        _layers.Draw(0, () => Add(batch, 1, 2, 3));
        await Frame(batch, 10);
        await Assert.That(slots.Shown).IsEquivalentTo([1, 2, 3, 10], Ordered);

        slots.Sent.Clear();
        _layers.Draw(0, () => Add(batch, 1));
        await Frame(batch, 10, 11, 12);

        await Assert.That(slots.Shown).IsEquivalentTo([1, 10, 11, 12], Ordered);
        await Assert.That(slots.Sent).IsEquivalentTo(["update 1 10", "update 2 11", "update 3 12"], Ordered).Because("the first slot holds what it held");
    }

    [Test]
    public async Task Slots_left_over_from_a_busier_frame_are_hidden_and_filled_again_later()
    {
        var (batch, slots) = Batch();
        await Frame(batch, 1, 2, 3);
        await Frame(batch, 4);
        await Assert.That(slots.Shown).IsEquivalentTo([4, Hidden, Hidden], Ordered);

        await Frame(batch, 5, 6);
        await Assert.That(slots.Shown).IsEquivalentTo([5, 6, Hidden], Ordered);
        await Assert.That(slots.Sent.Count(s => s.StartsWith("add "))).IsEqualTo(3);
    }

    [Test]
    public async Task A_batch_made_while_a_layer_is_drawn_joins_the_layer_and_keeps_it_over_later_frames()
    {
        InstanceBatch<IVertexData.None, Instance>? made = null;
        RecordingSlots? madeSlots = null;
        _layers.Draw(0, () =>
        {
            (made, madeSlots) = Batch();
            return Add(made, 7);
        });

        await Frame(made!, 8);
        await Frame(made!);
        await Assert.That(madeSlots!.Shown).IsEquivalentTo([7, Hidden], Ordered);
    }

    [Test]
    public async Task A_memo_replays_what_it_drew_into_the_batches_it_drew_into_while_its_key_is_the_same()
    {
        var (a, aSlots) = Batch();
        var (b, bSlots) = Batch();
        var memo = new Memo<int>();
        InstanceBatch<IVertexData.None, Instance>? made = null;
        RecordingSlots? madeSlots = null;
        var draws = 0;
        void Draw(int key)
        {
            _layers.Draw(memo, key, 0, _ =>
            {
                draws++;
                b.Add(new Instance(20 + key));
                // A batch made while the memo draws: what it drew there is kept too
                if (made is null)
                {
                    (made, madeSlots) = Batch();
                }

                made.Add(new Instance(30 + key));
            });
        }

        foreach (var key in new[] { 1, 1, 2 })
        {
            Begin(a, b);
            made?.Begin();
            a.Add(new Instance(10));
            Draw(key);
            await End(a, b, made!);
        }

        await Assert.That(draws).IsEqualTo(2).Because("the second frame's key was the same as the first's");
        await Assert.That(aSlots.Shown).IsEquivalentTo([10], Ordered);
        await Assert.That(bSlots.Shown).IsEquivalentTo([22], Ordered);
        await Assert.That(madeSlots!.Shown).IsEquivalentTo([32], Ordered);
        await Assert.That(bSlots.Sent).IsEquivalentTo(["add 0 21", "update 0 22"], Ordered).Because("the replayed frame sent nothing");
    }

    private static readonly TUnit.Assertions.Enums.CollectionOrdering Ordered = TUnit.Assertions.Enums.CollectionOrdering.Matching;

    private (InstanceBatch<IVertexData.None, Instance> Batch, RecordingSlots Slots) Batch()
    {
        var slots = new RecordingSlots();
        return (_layers.Add(new InstanceBatch<IVertexData.None, Instance>(slots, new Instance(Hidden))), slots);
    }

    private static Result Add(InstanceBatch<IVertexData.None, Instance> batch, params int[] values)
    {
        foreach (var value in values)
        {
            batch.Add(new Instance(value));
        }

        return Result.Success();
    }

    private static void Begin(params InstanceBatch<IVertexData.None, Instance>[] batches)
    {
        foreach (var batch in batches)
        {
            batch.Begin();
        }
    }

    private static async Task End(params InstanceBatch<IVertexData.None, Instance>[] batches)
    {
        foreach (var batch in batches)
        {
            await Assert.That(batch.End()).Succeeded();
        }
    }

    private static async Task Frame(InstanceBatch<IVertexData.None, Instance> batch, params int[] values)
    {
        batch.Begin();
        Add(batch, values);
        await End(batch);
    }

    private readonly record struct Instance(int Value) : IInstanceData
    {
        public void WriteTo(Span<float> buffer)
        {
        }

        public static int FloatCount => 0;

        public static VertexBufferLayout Layout => throw new NotSupportedException();
    }

    // A group's slots: what each shows, and what was sent to them, in order
    private sealed class RecordingSlots : IInstanceSlots<Instance>
    {
        private readonly List<InstanceId<Instance>> _ids = [];

        public List<int> Shown { get; } = [];

        public List<string> Sent { get; } = [];

        public Result<InstanceId<Instance>> Add(Instance instance)
        {
            var id = InstanceId.New<Instance>();
            Sent.Add($"add {_ids.Count} {instance.Value}");
            _ids.Add(id);
            Shown.Add(instance.Value);
            return id;
        }

        public Result Update(InstanceId<Instance> id, Instance instance)
        {
            var slot = _ids.IndexOf(id);
            Sent.Add($"update {slot} {instance.Value}");
            Shown[slot] = instance.Value;
            return Result.Success();
        }
    }
}
