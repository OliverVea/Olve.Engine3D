namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// What the scope's screen shows: the latest stretch of the signal, ten divisions long, starting where the signal rises
/// through zero so a steady tone stands still. A signal that never crosses runs free, its latest stretch shown as it comes.
/// </summary>
public static class ScopeTrace
{
    public const int Divisions = 10;
    public const int VerticalDivisions = 8;

    /// <summary>The most samples a read takes, so a scope reads a bounded amount however long its time per division.</summary>
    public const int MaxSamples = 32768;

    // How far, as a share of its peak, the signal must fall below zero before a rise through zero counts, so noise riding
    // on a crossing doesn't trigger it again
    private const float Hysteresis = 0.1f;

    // Below this the signal is silence, and a crossing in it only noise
    private const float Silence = 1e-4f;

    /// <summary>The samples the screen spans at a time per division in milliseconds.</summary>
    public static float Window(float millisecondsPerDivision, float sampleRate) => millisecondsPerDivision / 1000 * Divisions * sampleRate;

    /// <summary>The samples to read: the window and as long again before it, to find a crossing in.</summary>
    public static int SamplesToRead(float millisecondsPerDivision, float sampleRate) =>
        int.Clamp((int)MathF.Ceiling(2 * Window(millisecondsPerDivision, sampleRate)) + 2, 4, MaxSamples);

    /// <summary>
    /// Fills <paramref name="points"/> with the trace, evenly across the window, oldest sample first: the signal at each,
    /// from -1 at the bottom of the screen to 1 at the top, held at the edges past those.
    /// </summary>
    public static void Trace(ReadOnlySpan<float> samples, float window, Span<float> points)
    {
        if (samples.Length < 2)
        {
            points.Clear();
            return;
        }

        window = float.Clamp(window, 1, samples.Length - 1);
        var start = Trigger(samples, window) ?? samples.Length - 1 - window;
        for (var i = 0; i < points.Length; i++)
        {
            var at = start + window * i / int.Max(1, points.Length - 1);
            var below = int.Min((int)at, samples.Length - 2);
            points[i] = float.Clamp(float.Lerp(samples[below], samples[below + 1], at - below), -1, 1);
        }
    }

    // Where the latest rise through zero is that leaves a whole window after it, between samples, or null if there's none
    private static float? Trigger(ReadOnlySpan<float> samples, float window)
    {
        var last = samples.Length - 1 - window;
        var peak = 0f;
        foreach (var sample in samples)
        {
            peak = float.Max(peak, float.Abs(sample));
        }

        if (peak < Silence)
        {
            return null;
        }

        float? trigger = null;
        var armed = false;
        for (var i = 1; i < samples.Length && i - 1 <= last; i++)
        {
            armed |= samples[i - 1] < -Hysteresis * peak;
            if (armed && samples[i - 1] < 0 && samples[i] >= 0)
            {
                var crossing = i - 1 + samples[i - 1] / (samples[i - 1] - samples[i]);
                if (crossing <= last)
                {
                    trigger = crossing;
                }

                armed = false;
            }
        }

        return trigger;
    }
}
