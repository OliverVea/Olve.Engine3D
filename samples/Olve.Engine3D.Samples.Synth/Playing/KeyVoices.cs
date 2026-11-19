namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// The keys held and the voices they play, as many voices as the keyboard's VOICES selector says. With one, the keyboard
/// is monophonic (<see cref="HeldNotes"/>): the newest key sounds, and releasing it returns to an older key still held.
/// With more, each key takes a voice of its own (<see cref="VoiceAllocator"/>), the oldest note giving its voice up when
/// they are all playing.
/// </summary>
public sealed class KeyVoices
{
    private readonly HeldNotes _mono = new();
    private VoiceAllocator? _poly = new(Patching.Modules.MaxVoices);

    public int Voices { get; private set; } = Patching.Modules.MaxVoices;

    /// <summary>The keys held down, oldest first.</summary>
    public IReadOnlyList<int> Held => _poly?.Held ?? _mono.Held;

    /// <summary>The voices playing a key, and the key each plays.</summary>
    public IEnumerable<(int Voice, int Semitone)> Playing => _poly is { } poly
        ? poly.Playing
        : _mono.Sounding is { } sounding ? [(0, sounding)] : [];

    public VoiceChange? Press(int semitone) => _poly is { } poly ? poly.Press(semitone) : _mono.Press(semitone);

    public VoiceChange? Release(int semitone) => _poly is { } poly ? poly.Release(semitone) : _mono.Release(semitone);

    public IReadOnlyList<VoiceChange> ReleaseAll() => _poly is { } poly
        ? poly.ReleaseAll()
        : _mono.ReleaseAll() is { } stop ? [stop] : [];

    /// <summary>
    /// Plays on this many voices from now on, from 1 to <see cref="Patching.Modules.MaxVoices"/>. Returns the old voices
    /// stopped; the keys held stay held, oldest first, on the new voices (<see cref="Playing"/>), for the caller to start
    /// once those voices exist.
    /// </summary>
    public IReadOnlyList<VoiceChange> SetVoices(int voices)
    {
        var held = Held.ToArray();
        var released = ReleaseAll();
        Voices = int.Clamp(voices, 1, Patching.Modules.MaxVoices);
        _poly = Voices > 1 ? new VoiceAllocator(Voices) : null;
        foreach (var semitone in held)
        {
            Press(semitone);
        }

        return released;
    }
}
