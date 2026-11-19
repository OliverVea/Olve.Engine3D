using Olve.Engine3D.Audio.Synthesis;

namespace Olve.Engine3D.Audio;

/// <summary>
/// An Audio EQ Cookbook biquad, read as Web Audio reads it: Q in dB for lowpass and highpass, linear for the others
/// (shelves ignore it); the gain in dB for peaking and the shelves.
/// </summary>
internal struct Biquad
{
    private double _b0, _b1, _b2, _a1, _a2;
    private double _x1, _x2, _y1, _y2;

    public void SetCoefficients(FilterType type, double cutoff, double q, double gainDb, int sampleRate)
    {
        var nyquist = sampleRate / 2.0;
        var frequency = double.Clamp(cutoff, 10, nyquist * 0.99);
        var w0 = double.Tau * frequency / sampleRate;
        var cos = double.Cos(w0);
        var sin = double.Sin(w0);
        var a = double.Pow(10, gainDb / 40);

        var alpha = type switch
        {
            FilterType.Lowpass or FilterType.Highpass => sin / (2 * double.Pow(10, q / 20)),
            FilterType.Lowshelf or FilterType.Highshelf => sin / 2 * double.Sqrt(2),
            _ => sin / (2 * double.Max(q, 1e-4)),
        };
        var shelf = 2 * double.Sqrt(a) * alpha;

        var (b0, b1, b2, a0, a1, a2) = type switch
        {
            FilterType.Lowpass => ((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha),
            FilterType.Highpass => ((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha),
            FilterType.Bandpass => (alpha, 0.0, -alpha, 1 + alpha, -2 * cos, 1 - alpha),
            FilterType.Notch => (1, -2 * cos, 1, 1 + alpha, -2 * cos, 1 - alpha),
            FilterType.Allpass => (1 - alpha, -2 * cos, 1 + alpha, 1 + alpha, -2 * cos, 1 - alpha),
            FilterType.Peaking => (1 + alpha * a, -2 * cos, 1 - alpha * a, 1 + alpha / a, -2 * cos, 1 - alpha / a),
            FilterType.Lowshelf => (
                a * ((a + 1) - (a - 1) * cos + shelf),
                2 * a * ((a - 1) - (a + 1) * cos),
                a * ((a + 1) - (a - 1) * cos - shelf),
                (a + 1) + (a - 1) * cos + shelf,
                -2 * ((a - 1) + (a + 1) * cos),
                (a + 1) + (a - 1) * cos - shelf),
            _ => (
                a * ((a + 1) + (a - 1) * cos + shelf),
                -2 * a * ((a - 1) + (a + 1) * cos),
                a * ((a + 1) + (a - 1) * cos - shelf),
                (a + 1) - (a - 1) * cos + shelf,
                2 * ((a - 1) - (a + 1) * cos),
                (a + 1) - (a - 1) * cos - shelf),
        };

        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
    }

    public double Process(double x)
    {
        var y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
        _x2 = _x1;
        _x1 = x;
        _y2 = _y1;
        _y1 = y;
        return y;
    }
}
