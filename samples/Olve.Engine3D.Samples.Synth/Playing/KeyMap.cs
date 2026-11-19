using Olve.Engine3D.Input;

namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// The computer keyboard as a piano: the home row plays the white keys from C, the row above the black keys, on past
/// the octave as far as the keys go (C to F).
/// </summary>
public static class KeyMap
{
    public static readonly IReadOnlyList<Key> Notes =
    [
        Key.A, Key.W, Key.S, Key.E, Key.D, Key.F, Key.T, Key.G, Key.Y, Key.H, Key.U, Key.J,
        Key.K, Key.O, Key.L, Key.P, Key.Semicolon, Key.Apostrophe,
    ];

    public const Key OctaveDown = Key.Z;
    public const Key OctaveUp = Key.X;

    private static readonly HashSet<int> BlackSemitones = [1, 3, 6, 8, 10];

    /// <summary>The computer key that plays a semitone, as the piano labels it, or null when none does.</summary>
    public static string? Label(int semitone) => semitone < Notes.Count
        ? Notes[semitone] switch
        {
            Key.Semicolon => ";",
            Key.Apostrophe => "'",
            var key => key.ToString(),
        }
        : null;

    /// <summary>The semitone a computer key plays, or null when it plays none.</summary>
    public static int? SemitoneOf(Key key)
    {
        for (var semitone = 0; semitone < Notes.Count; semitone++)
        {
            if (Notes[semitone] == key)
            {
                return semitone;
            }
        }

        return null;
    }

    public static bool IsBlack(int semitone) => BlackSemitones.Contains(((semitone % 12) + 12) % 12);
}
