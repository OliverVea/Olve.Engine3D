namespace Olve.Engine3D.Audio;

/// <summary>A sound, rendered in advance: mono samples at a sample rate. Never changes once shared.</summary>
internal sealed record BakedSound(float[] Samples, int SampleRate);

/// <summary>What a voice plays: samples by index, at the source's own rate. Audio thread only.</summary>
internal abstract class VoiceSource
{
    public abstract int SampleRate { get; }

    /// <summary>The sample at the index; silence before the start.</summary>
    public abstract float At(long index);

    /// <summary>Whether nothing is left to play from the position on.</summary>
    public abstract bool HasEnded(double position);

    /// <summary>The position to carry on from, for a source that loops.</summary>
    public virtual double Wrap(double position) => position;

    /// <summary>Stops looping from the index: the sound plays to its end.</summary>
    public abstract void Release(long index);
}

/// <summary>A sound rendered in advance.</summary>
internal sealed class BakedSource(BakedSound sound, bool loop) : VoiceSource
{
    private bool _loop = loop;

    public override int SampleRate => sound.SampleRate;

    public override float At(long index)
    {
        var samples = sound.Samples;
        return _loop
            ? samples[(int)(((index % samples.Length) + samples.Length) % samples.Length)]
            : samples[(int)long.Clamp(index, 0, samples.Length - 1)];
    }

    public override bool HasEnded(double position) => !_loop && position >= sound.Samples.Length - 1;

    public override double Wrap(double position) =>
        _loop && position >= sound.Samples.Length ? position % sound.Samples.Length : position;

    public override void Release(long index) => _loop = false;
}
