namespace Olve.Engine3D.Audio;

/// <summary>
/// Samples for a number of frames, one contiguous span per channel (planar). Reused between frames, so its storage
/// only grows.
/// </summary>
public sealed class SampleBuffer
{
    private float[] _samples = [];

    public int Channels { get; private set; }

    public int Frames { get; private set; }

    /// <summary>The channel's samples, one per frame.</summary>
    public Span<float> GetChannel(int channel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(channel, Channels);
        return _samples.AsSpan(channel * Frames, Frames);
    }

    /// <summary>Sets the buffer's shape and fills it with silence.</summary>
    public void Reset(int channels, int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(channels);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);

        var length = channels * frames;
        if (_samples.Length < length)
        {
            _samples = new float[length];
        }

        Channels = channels;
        Frames = frames;
        _samples.AsSpan(0, length).Clear();
    }
}
