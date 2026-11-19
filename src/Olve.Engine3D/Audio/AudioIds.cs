using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio;

/// <summary>A sound the <see cref="SoundManager"/> holds; it plays once it has rendered.</summary>
public readonly record struct SoundId(Id Value)
{
    public static SoundId New() => new(Id.New());
    public override string ToString() => Value.ToString();
}

/// <summary>A sound playing, from <see cref="AudioMixer.Play"/> until it ends or is stopped.</summary>
public readonly record struct VoiceId(Id Value)
{
    public static VoiceId New() => new(Id.New());
    public override string ToString() => Value.ToString();
}

/// <summary>A group of voices mixed together, with its own gain and filter.</summary>
public readonly record struct BusId(Id Value)
{
    public static BusId New() => new(Id.New());
    public override string ToString() => Value.ToString();
}

public readonly record struct BusName(string Value)
{
    public override string ToString() => Value;
}

/// <param name="Gain">Linear volume.</param>
/// <param name="Pan">From -1 (left) to 1 (right).</param>
/// <param name="Pitch">The playback rate: 2 plays an octave up and twice as fast.</param>
/// <param name="Loop">Whether the sound starts over until stopped.</param>
public sealed record VoiceSettings(float Gain = 1, float Pan = 0, float Pitch = 1, bool Loop = false);

public enum VoiceParameter
{
    Gain,
    Pan,
    Pitch,
}

public enum BusParameter
{
    /// <summary>Linear volume.</summary>
    Gain,

    /// <summary>The cutoff of the bus's lowpass, in Hz; at the top of the range it lets everything through.</summary>
    Cutoff,
}
