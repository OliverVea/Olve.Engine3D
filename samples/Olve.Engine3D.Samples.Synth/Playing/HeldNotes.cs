namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// The keys held down, by semitone, for a monophonic keyboard, played on voice 0: the newest key sounds, and each key
/// pressed opens the gate again. Releasing the sounding key falls back to the newest key still held, at its pitch without
/// opening the gate again; releasing the last closes the gate.
/// </summary>
public sealed class HeldNotes
{
    private readonly List<int> _held = [];

    /// <summary>The keys held down, oldest first.</summary>
    public IReadOnlyList<int> Held => _held;

    public int? Sounding => _held.Count > 0 ? _held[^1] : null;

    public VoiceChange Press(int semitone)
    {
        _held.Remove(semitone);
        _held.Add(semitone);
        return new VoiceChange.Play(0, semitone);
    }

    /// <summary>The change, or null when the key released wasn't the one sounding.</summary>
    public VoiceChange? Release(int semitone)
    {
        var wasSounding = Sounding == semitone;
        if (!_held.Remove(semitone) || !wasSounding)
        {
            return null;
        }

        return Sounding is { } previous ? new VoiceChange.Play(0, previous, Retrigger: false) : new VoiceChange.Stop(0);
    }

    public VoiceChange? ReleaseAll()
    {
        if (_held.Count == 0)
        {
            return null;
        }

        _held.Clear();
        return new VoiceChange.Stop(0);
    }
}
