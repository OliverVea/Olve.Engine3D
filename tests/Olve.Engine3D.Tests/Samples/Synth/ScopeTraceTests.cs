using Olve.Engine3D.Samples.Synth.Playing;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class ScopeTraceTests
{
    private const float Rate = 48000;

    private static float[] Sine(float hertz, float phase, int count) =>
        [.. Enumerable.Range(0, count).Select(i => 0.8f * MathF.Sin(2 * MathF.PI * hertz * i / Rate + phase))];

    [Test]
    [Arguments(0f)]
    [Arguments(1.3f)]
    [Arguments(4f)]
    public async Task A_steady_tone_starts_where_it_rises_through_zero_whatever_its_phase(float phase)
    {
        var window = ScopeTrace.Window(2, Rate);
        var points = new float[64];
        ScopeTrace.Trace(Sine(440, phase, ScopeTrace.SamplesToRead(2, Rate)), window, points);

        await Assert.That(points[0]).IsEqualTo(0).Within(0.02f);
        await Assert.That(points[1]).IsGreaterThan(0);
    }

    [Test]
    public async Task A_signal_that_never_crosses_runs_free_and_silence_is_flat()
    {
        var points = new float[16];
        ScopeTrace.Trace(Enumerable.Repeat(0.5f, 4000).ToArray(), 1000, points);
        await Assert.That(points.All(p => p == 0.5f)).IsTrue();

        ScopeTrace.Trace(new float[4000], 1000, points);
        await Assert.That(points.All(p => p == 0)).IsTrue();
    }
}
