using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio;

/// <summary>
/// The sounds the game plays: patches rendered once, within a budget each frame (<see cref="SoundRenderBudget"/>), and ready to play once rendered.
/// Main thread only.
/// </summary>
public sealed class SoundManager(
    TimeProvider timeProvider,
    SoundRenderBudget budget,
    ISoundBackend backend,
    ILogger<SoundManager> logger)
{
    /// <summary>The rate sounds are rendered at; voices convert it to the output's.</summary>
    public const int SampleRate = 48_000;

    private readonly HashSet<SoundId> _sounds = [];
    private readonly Dictionary<SoundId, PatchRendering> _rendering = [];
    private readonly Queue<SoundId> _queue = [];
    private readonly Dictionary<Id<AudioSegment>, List<SoundId>> _segments = [];

    public int Count => _sounds.Count;

    public int RenderingCount => _rendering.Count;

    /// <summary>
    /// Compiles the patch and queues its note, held for <paramref name="gate"/>, to render over the next frames. The
    /// sound is kept until the segment is released.
    /// </summary>
    public Result<SoundId> Register(Patch patch, TimeSpan gate, Id<AudioSegment> segment,
        IReadOnlyDictionary<string, float>? parameters = null)
    {
        if (PatchRenderer.Start(patch, SampleRate, gate, parameters).TryPickProblems(out var problems, out var rendering))
        {
            return problems.Prepend("Failed to render the sound");
        }

        var id = SoundId.New();
        _rendering[id] = rendering;
        _queue.Enqueue(id);
        if (!_segments.TryGetValue(segment, out var owned))
        {
            _segments[segment] = owned = [];
        }

        owned.Add(id);
        logger.LogDebug("Registered sound {SoundId} ({Frames} frames to render)", id, rendering.Buffer.Frames);
        return id;
    }

    public void Update()
    {
        var start = timeProvider.GetTimestamp();
        while (_queue.TryPeek(out var id))
        {
            if (!_rendering.TryGetValue(id, out var rendering))
            {
                _queue.Dequeue();
                continue;
            }

            rendering.RenderBlock();
            if (rendering.IsComplete)
            {
                _queue.Dequeue();
                _rendering.Remove(id);
                Complete(id, rendering);
            }

            if (timeProvider.GetElapsedTime(start) >= budget.PerFrame)
            {
                return;
            }
        }
    }

    public DeletionResult Delete(SoundId id)
    {
        if (_sounds.Remove(id))
        {
            _ = backend.DeleteSound(id);
        }
        else if (!_rendering.Remove(id))
        {
            return DeletionResult.NotFound();
        }

        foreach (var owned in _segments.Values)
        {
            owned.Remove(id);
        }

        logger.LogDebug("Deleted sound {SoundId}", id);
        return DeletionResult.Success();
    }

    public bool IsRendering(SoundId id) => _rendering.ContainsKey(id);

    internal Result CheckReady(SoundId id)
    {
        if (_sounds.Contains(id))
        {
            return Result.Success();
        }

        return _rendering.ContainsKey(id)
            ? new ResultProblem("Sound {0} is still rendering", id)
            : new ResultProblem("No sound {0}", id);
    }

    /// <summary>Deletes the segment's sounds, rendered or not; voices already playing them play on.</summary>
    public void DeleteSegment(Id<AudioSegment> segment)
    {
        if (!_segments.Remove(segment, out var owned))
        {
            return;
        }

        foreach (var id in owned)
        {
            if (_sounds.Remove(id))
            {
                _ = backend.DeleteSound(id);
            }

            _rendering.Remove(id);
        }

        logger.LogDebug("Deleted {Count} sounds of audio segment {SegmentId}", owned.Count, segment);
    }

    private void Complete(SoundId id, PatchRendering rendering)
    {
        if (backend.CreateSound(id, rendering.Buffer.GetChannel(0), SampleRate).TryPickProblems(out var problems))
        {
            logger.LogWarning("Failed to create sound {SoundId}: {Problems}", id, problems);
            foreach (var owned in _segments.Values)
            {
                owned.Remove(id);
            }

            return;
        }

        _sounds.Add(id);
        logger.LogDebug("Rendered sound {SoundId} ({Frames} frames)", id, rendering.Buffer.Frames);
    }
}
