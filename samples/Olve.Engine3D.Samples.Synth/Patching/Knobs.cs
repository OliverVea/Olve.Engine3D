using System.Globalization;

namespace Olve.Engine3D.Samples.Synth.Patching;

/// <summary>
/// How a knob's turn, from 0 (all the way left) to 1 (all the way right), maps to its value, how the value reads, and how
/// a signal in the knob's CV input turns it.
/// </summary>
public static class Knobs
{
    /// <summary>How far one unit of signal in a knob's CV input turns the knob, unless its module says otherwise: half its sweep.</summary>
    public const float CvTurn = 0.5f;

    /// <summary>
    /// The value a knob has with a signal in its CV input: the knob sets the base, and the signal adds to its turn,
    /// <see cref="ParameterDefinition.CvTurn"/> per unit, stopping at either end. So an LFO sweeps a frequency or a time by equal ratios, as
    /// turning the knob by hand would. The signal moves a stepped knob smoothly between its steps, not just to them.
    /// </summary>
    public static float Modulate(ParameterDefinition parameter, float value, float cv) =>
        Continuous(parameter, ToTurn(parameter, value) + cv * parameter.CvTurn);

    /// <summary>
    /// <see cref="Modulate"/> as a table for the audio to look the value up in, by the turn from -1 (all the way left) to
    /// 1 (all the way right): an odd number of points, evenly spaced, so the middle one is exact. Feeding it the knob's
    /// turn as <c>2 * turn - 1</c> plus the signal times <see cref="CvGain"/> gives the modulated value, clamped at the
    /// ends of the table as at the ends of the knob.
    /// </summary>
    public static float[] Curve(ParameterDefinition parameter, int points = 1025)
    {
        var curve = new float[points | 1];
        for (var i = 0; i < curve.Length; i++)
        {
            curve[i] = Continuous(parameter, i / (float)(curve.Length - 1));
        }

        return curve;
    }

    /// <summary>How much of a unit of CV signal goes into the turn that <see cref="Curve"/> is looked up by.</summary>
    public static float CvGain(ParameterDefinition parameter) => 2 * parameter.CvTurn;

    public static float ToTurn(ParameterDefinition parameter, float value)
    {
        var turn = parameter.Scale == ParameterScale.Exponential
            ? MathF.Log(value / parameter.Min) / MathF.Log(parameter.Max / parameter.Min)
            : (value - parameter.Min) / (parameter.Max - parameter.Min);
        return float.Clamp(turn, 0, 1);
    }

    public static float FromTurn(ParameterDefinition parameter, float turn) => parameter.Scale == ParameterScale.Stepped
        ? MathF.Round(Continuous(parameter, turn))
        : Continuous(parameter, turn);

    // The value at a turn, without rounding a stepped knob to its steps
    private static float Continuous(ParameterDefinition parameter, float turn)
    {
        turn = float.Clamp(turn, 0, 1);
        return parameter.Scale == ParameterScale.Exponential
            ? parameter.Min * MathF.Pow(parameter.Max / parameter.Min, turn)
            : parameter.Min + turn * (parameter.Max - parameter.Min);
    }

    /// <summary>The value as the panel shows it, in capitals: "SAW", "1.2K HZ", "+7 ST", "0.60".</summary>
    public static string Format(ParameterDefinition parameter, float value)
    {
        if (parameter.HasChoices)
        {
            return parameter.Choices[(int)float.Clamp(value - parameter.Min, 0, parameter.Choices.Length - 1)];
        }

        var number = parameter.Scale == ParameterScale.Stepped
            ? ((int)value).ToString(parameter.Min < 0 ? "+0;-0;0" : "0", CultureInfo.InvariantCulture)
            : FormatNumber(value);
        return parameter.Unit.Length == 0 ? number : $"{number} {parameter.Unit}";
    }

    private static string FormatNumber(float value) => float.Abs(value) switch
    {
        >= 1000 => (value / 1000).ToString(value >= 10000 ? "0.#" : "0.##", CultureInfo.InvariantCulture) + "K",
        >= 100 => value.ToString("0", CultureInfo.InvariantCulture),
        >= 10 => value.ToString("0.#", CultureInfo.InvariantCulture),
        _ => value.ToString("0.00", CultureInfo.InvariantCulture),
    };
}
