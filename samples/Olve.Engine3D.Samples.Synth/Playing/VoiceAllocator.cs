namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>What a voice should do after a key goes down or up.</summary>
public abstract record VoiceChange
{
    private VoiceChange()
    {
    }

    /// <summary>Play this semitone from the keyboard's C on the voice; with retrigger, its gate opens again and its envelopes restart.</summary>
    public sealed record Play(int Voice, int Semitone, bool Retrigger = true) : VoiceChange;

    /// <summary>The voice's key is up: its gate closes and its envelopes release.</summary>
    public sealed record Stop(int Voice) : VoiceChange;
}

/// <summary>
/// The keys held down and the voices playing them, so the keyboard plays chords. A key pressed takes the voice released
/// longest ago, so the others' release tails ring on; with every voice playing, the oldest note gives its voice up, and
/// its key stays down but silent. Pressing a key already playing starts its voice again.
/// </summary>
public sealed class VoiceAllocator(int voices)
{
    private readonly List<int> _held = [];
    // Per voice: the key it plays, or null once released, and when it last started or released
    private readonly int?[] _semitones = new int?[voices];
    private readonly long[] _changed = new long[voices];
    private long _clock;

    /// <summary>The keys held down, oldest first, including any whose voice was taken.</summary>
    public IReadOnlyList<int> Held => _held;

    /// <summary>The voices playing a key, and the key each plays.</summary>
    public IEnumerable<(int Voice, int Semitone)> Playing =>
        _semitones.Select((semitone, voice) => (voice, semitone)).Where(v => v.semitone is not null).Select(v => (v.voice, v.semitone!.Value));

    public VoiceChange Press(int semitone)
    {
        _held.Remove(semitone);
        _held.Add(semitone);
        var voice = VoiceOf(semitone) ?? Oldest(free: true) ?? Oldest(free: false)!.Value;
        _semitones[voice] = semitone;
        _changed[voice] = ++_clock;
        return new VoiceChange.Play(voice, semitone);
    }

    /// <summary>The change, or null when the key wasn't down or its voice was taken.</summary>
    public VoiceChange? Release(int semitone)
    {
        if (!_held.Remove(semitone) || VoiceOf(semitone) is not { } voice)
        {
            return null;
        }

        _semitones[voice] = null;
        _changed[voice] = ++_clock;
        return new VoiceChange.Stop(voice);
    }

    public IReadOnlyList<VoiceChange> ReleaseAll() => [.. _held.ToList().Select(Release).OfType<VoiceChange>()];

    private int? VoiceOf(int semitone)
    {
        var voice = Array.IndexOf(_semitones, semitone);
        return voice >= 0 ? voice : null;
    }

    // The free or the playing voice that changed longest ago
    private int? Oldest(bool free) => Enumerable.Range(0, voices)
        .Where(v => _semitones[v] is null == free)
        .Select(v => (int?)v)
        .MinBy(v => _changed[v!.Value]);
}
