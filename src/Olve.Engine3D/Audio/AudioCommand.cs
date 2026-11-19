namespace Olve.Engine3D.Audio;

/// <summary>What the <see cref="AudioMixer"/> asks of an output; the output carries it out in order.</summary>
public abstract record AudioCommand
{
    // Closed: the cases below are the only ones.
    private AudioCommand()
    {
    }

    /// <summary>Removes the bus once the voices playing on it have ended.</summary>
    public sealed record RemoveBus(BusId Bus) : AudioCommand;

    public sealed record Play(VoiceId Voice, SoundId Sound, BusId Bus, VoiceSettings Settings) : AudioCommand;

    public sealed record Release(VoiceId Voice) : AudioCommand;

    public sealed record SetVoice(VoiceId Voice, VoiceParameter Parameter, float Value, TimeSpan Ramp) : AudioCommand;

    public sealed record StopVoice(VoiceId Voice, TimeSpan Fade) : AudioCommand;

    public sealed record SetBus(BusId Bus, BusParameter Parameter, float Value, TimeSpan Ramp) : AudioCommand;
}
