namespace Olve.Engine3D.Audio.Synthesis;

/// <summary>Renders a patch offline: one note, held for a time, then released.</summary>
public static class PatchRenderer
{
    /// <summary>The frames rendered at a time, as Web Audio renders.</summary>
    public const int BlockFrames = 128;

    /// <summary>
    /// Renders a mono note held for <paramref name="gate"/>, plus the longest envelope release after it. Renders the
    /// same samples for the same arguments.
    /// </summary>
    public static Result<SampleBuffer> Render(
        Patch patch,
        int sampleRate,
        TimeSpan gate,
        IReadOnlyDictionary<string, float>? parameters = null)
    {
        if (Start(patch, sampleRate, gate, parameters).TryPickProblems(out var problems, out var rendering))
        {
            return problems;
        }

        while (!rendering.IsComplete)
        {
            rendering.RenderBlock();
        }

        return rendering.Buffer;
    }

    public static Result<PatchRendering> Start(
        Patch patch,
        int sampleRate,
        TimeSpan gate,
        IReadOnlyDictionary<string, float>? parameters = null)
    {
        if (sampleRate <= 0)
        {
            return new ResultProblem("The sample rate must be positive, not {0}", sampleRate);
        }

        if (gate < TimeSpan.Zero)
        {
            return new ResultProblem("The note can't be held for a negative time ({0})", gate);
        }

        if (PatchVoice.Compile(patch, sampleRate, parameters ?? new Dictionary<string, float>())
            .TryPickProblems(out var problems, out var voice))
        {
            return problems.Prepend("Failed to compile the patch");
        }

        var gateFrames = (long)double.Round(gate.TotalSeconds * sampleRate);
        return new PatchRendering(voice, gateFrames, (int)(gateFrames + voice.ReleaseFrames));
    }
}

public sealed class PatchRendering
{
    private readonly PatchVoice _voice;
    private readonly long _gateFrames;
    private int _rendered;

    internal PatchRendering(PatchVoice voice, long gateFrames, int totalFrames)
    {
        _voice = voice;
        _gateFrames = gateFrames;
        Buffer.Reset(1, totalFrames);
    }

    public SampleBuffer Buffer { get; } = new();

    public bool IsComplete => _rendered >= Buffer.Frames;

    public void RenderBlock()
    {
        if (IsComplete)
        {
            return;
        }

        var frames = int.Min(PatchRenderer.BlockFrames, Buffer.Frames - _rendered);
        _voice.Process(_rendered, frames, _gateFrames, Buffer.GetChannel(0).Slice(_rendered, frames));
        _rendered += frames;
    }
}
