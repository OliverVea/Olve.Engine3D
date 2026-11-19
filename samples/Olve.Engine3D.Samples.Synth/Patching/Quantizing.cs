namespace Olve.Engine3D.Samples.Synth.Patching;

/// <summary>
/// The quantizer's sums: a pitch, in octaves from middle C, pulled to the nearest note of one of
/// <see cref="Modules.Scales"/>, and that as a table for the audio to look up.
/// </summary>
public static class Quantizing
{
    /// <summary>How far the table reaches either way of middle C, in octaves; a pitch past it gives the note at its end.</summary>
    public const float Span = 5;

    /// <summary>The table's points: odd, so the middle one is middle C, and dense enough that the step between two notes is under 2 cents wide.</summary>
    public const int Points = 8193;

    /// <summary>The note of the scale nearest the pitch, in octaves; halfway between two notes, the lower.</summary>
    public static float Quantize(float pitch, int scale)
    {
        var semitones = Modules.Scales[int.Clamp(scale, 0, Modules.Scales.Length - 1)].Semitones;
        var semitone = pitch * 12;
        var octave = MathF.Floor(semitone / 12);
        var best = float.NaN;
        // The scale's notes in this octave and the ones either side, so the nearest is found across an octave's edge
        foreach (var shift in (ReadOnlySpan<float>)[-12, 0, 12])
        {
            foreach (var note in semitones)
            {
                var candidate = octave * 12 + shift + note;
                var distance = MathF.Abs(candidate - semitone);
                if (float.IsNaN(best) || distance < MathF.Abs(best - semitone) - 1e-4f
                    || MathF.Abs(distance - MathF.Abs(best - semitone)) <= 1e-4f && candidate < best)
                {
                    best = candidate;
                }
            }
        }

        return best / 12;
    }

    /// <summary>
    /// The scale's quantizer as a table over its input divided by <see cref="Span"/>, from -1 to 1, as a wave shaper
    /// reads it: each point the note nearest the pitch there.
    /// </summary>
    public static float[] Table(int scale)
    {
        var table = new float[Points];
        for (var i = 0; i < Points; i++)
        {
            table[i] = Quantize((2f * i / (Points - 1) - 1) * Span, scale);
        }

        return table;
    }
}
