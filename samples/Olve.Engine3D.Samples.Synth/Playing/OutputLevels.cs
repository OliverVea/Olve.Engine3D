using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// A level meter's reading, in decibels of the signal's peak: it jumps up to a new peak at once and falls back slowly, so
/// a short burst still shows. Its ring of <see cref="Lights"/> lights shows from <see cref="Floor"/> to full scale.
/// </summary>
public static class OutputMeter
{
    public const int Lights = 12;

    /// <summary>The quietest level that lights the first light, in decibels.</summary>
    public const float Floor = -42;

    /// <summary>How fast the reading falls once the peak passes, in decibels a second.</summary>
    public const float Fall = 30;

    /// <summary>The reading after <paramref name="seconds"/>, given the peak heard meanwhile (its magnitude, 1 at full scale).</summary>
    public static float Step(float reading, float peak, float seconds)
    {
        var heard = peak > 0 ? 20 * MathF.Log10(peak) : float.NegativeInfinity;
        return float.Max(Floor - 1, float.Max(heard, reading - Fall * seconds));
    }

    /// <summary>How many lights the reading lights, from 0 to <see cref="Lights"/>: the last one lit only partly, by the fraction.</summary>
    public static float Lit(float reading) => float.Clamp((reading - Floor) / -Floor * Lights, 0, Lights);
}

/// <summary>
/// How loud the output is, from 0 for silence to 1, smoothed so it breathes with the music rather than flickering: it
/// rises over about <see cref="Attack"/> seconds and falls over about <see cref="Release"/>.
/// </summary>
public static class Loudness
{
    public const float Attack = 0.06f, Release = 0.45f;

    /// <summary>How loud the output's level (its RMS, about 1 at full scale) counts as at full loudness.</summary>
    public const float Full = 0.35f;

    public static float Step(float loudness, float level, float seconds)
    {
        var target = float.Clamp(level / Full, 0, 1);
        var time = target > loudness ? Attack : Release;
        return loudness + (target - loudness) * (1 - MathF.Exp(-seconds / time));
    }
}

/// <summary>
/// Each frame, every module's outputs are read back from the audio, their peaks since the last frame, and kept as
/// <see cref="OutputMeter"/> readings for the rings of lights round the output jacks. The audio meters only the outputs
/// the patch has.
/// </summary>
public sealed class OutputLevelsService(SynthRack rack, DeltaTimeService time) : ISceneService
{
    private List<JackRef> _outputs = [];
    private int _version = -1;
    private readonly Dictionary<JackRef, float> _readings = [];
    private float[] _peaks = [];

    /// <summary>How many lights an output's ring lights now, from 0 to <see cref="OutputMeter.Lights"/>.</summary>
    public float Lit(JackRef output) => OutputMeter.Lit(_readings.GetValueOrDefault(output, OutputMeter.Floor - 1));

    public Result Update()
    {
        if (rack.Patch.Version != _version)
        {
            _version = rack.Patch.Version;
            var outputs = rack.Patch.Modules
                .SelectMany(m => m.Definition.Jacks.Where(j => j.Direction == JackDirection.Output).Select(j => new JackRef(m.Id, j.Name)))
                .ToList();
            if (!outputs.SequenceEqual(_outputs))
            {
                _outputs = outputs;
                _peaks = new float[outputs.Count];
                rack.Audio.MeterOutputs(outputs);
            }
        }

        var read = rack.Audio.ReadLevels(_peaks);
        var seconds = (float)time.RawDeltaTime.TotalSeconds;
        for (var i = 0; i < _outputs.Count; i++)
        {
            var reading = _readings.GetValueOrDefault(_outputs[i], OutputMeter.Floor - 1);
            _readings[_outputs[i]] = OutputMeter.Step(reading, i < read ? _peaks[i] : 0, seconds);
        }

        return Result.Success();
    }
}
