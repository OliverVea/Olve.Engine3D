using Olve.Engine3D.Audio.Synthesis;
using Olve.Results;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Audio;

public class GnarlyNodeTests
{
    private const int SampleRate = 48_000;

    private static float[] Render(Patch patch, double seconds = 0.5)
    {
        var result = PatchRenderer.Render(patch, SampleRate, TimeSpan.FromSeconds(seconds));
        return result.TryPickProblems(out var problems, out var buffer)
            ? throw new InvalidOperationException(string.Join("; ", problems.Select(p => p.ToDebugString())))
            : buffer.GetChannel(0).ToArray();
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        var sum = 0.0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return double.Sqrt(sum / samples.Length);
    }

    private static double FilteredRms(float frequency, PatchNode.Filter filter)
    {
        var patch = new Patch(
            [new PatchNode.Oscillator("tone", Waveform.Sine, frequency), filter],
            [new PatchConnection("tone", filter.Name)],
            [],
            filter.Name);
        return Rms(Render(patch).AsSpan(SampleRate / 10));
    }

    private static readonly double SineRms = double.Sqrt(0.5);

    [Test]
    public async Task A_Notch_Removes_Its_Cutoff_And_Keeps_The_Rest()
    {
        await Assert.That(FilteredRms(1000, new("filter", FilterType.Notch, 1000, 1))).IsLessThan(0.01);
        await Assert.That(FilteredRms(8000, new("filter", FilterType.Notch, 1000, 1))).IsGreaterThan(0.65);
    }

    [Test]
    public async Task Peaking_And_Shelves_Boost_By_Their_Gain()
    {
        var boost = double.Pow(10, 12 / 20.0);

        await Assert.That(FilteredRms(1000, new("filter", FilterType.Peaking, 1000, 1, 12)) / SineRms).IsEqualTo(boost).Within(0.05);
        await Assert.That(FilteredRms(50, new("filter", FilterType.Lowshelf, 1000, 0, 12)) / SineRms).IsEqualTo(boost).Within(0.1);
        await Assert.That(FilteredRms(10_000, new("filter", FilterType.Lowshelf, 1000, 0, 12)) / SineRms).IsEqualTo(1).Within(0.05);
        await Assert.That(FilteredRms(15_000, new("filter", FilterType.Highshelf, 1000, 0, -12)) / SineRms).IsEqualTo(1 / boost).Within(0.05);
    }

    [Test]
    public async Task An_Allpass_Keeps_The_Level()
    {
        await Assert.That(FilteredRms(1000, new("filter", FilterType.Allpass, 1000, 1)) / SineRms).IsEqualTo(1).Within(0.01);
    }

    private static float[] Distorted(float frequency, DistortionCurve curve, float drive) => Render(new Patch(
        [new PatchNode.Oscillator("tone", Waveform.Sine, frequency), new PatchNode.Distortion("drive", curve, drive)],
        [new PatchConnection("tone", "drive")],
        [],
        "drive"), 0.5);

    // The amplitude of one frequency in the signal (a single bin of a Fourier transform)
    private static double Amplitude(ReadOnlySpan<float> samples, double frequency)
    {
        var (re, im) = (0.0, 0.0);
        for (var i = 0; i < samples.Length; i++)
        {
            var angle = double.Tau * frequency * i / SampleRate;
            re += samples[i] * double.Cos(angle);
            im -= samples[i] * double.Sin(angle);
        }

        return 2 * double.Sqrt(re * re + im * im) / samples.Length;
    }

    [Test]
    [Arguments(DistortionCurve.SoftClip)]
    [Arguments(DistortionCurve.HardClip)]
    public async Task Clipping_Squares_A_Driven_Sine_Within_Full_Scale(DistortionCurve curve)
    {
        var samples = Distorted(200, curve, 10).AsSpan(SampleRate / 10).ToArray();

        await Assert.That(samples.Max(float.Abs)).IsLessThan(1.1f);
        await Assert.That(Rms(samples)).IsGreaterThan(0.85);
    }

    [Test]
    public async Task Folding_Adds_Crossings_Within_Full_Scale()
    {
        var samples = Distorted(200, DistortionCurve.Fold, 3.5f).AsSpan(SampleRate / 10).ToArray();
        var crossings = samples.Zip(samples.Skip(1)).Count(pair => pair.First < 0 && pair.Second >= 0);

        await Assert.That(samples.Max(float.Abs)).IsLessThan(1.1f);
        await Assert.That(crossings).IsGreaterThanOrEqualTo(80 * 3);
    }

    [Test]
    public async Task Oversampling_Keeps_The_Harmonics_From_Aliasing()
    {
        // The 5th harmonic of 9 kHz is 45 kHz, which would fold back to 3 kHz at 48 kHz
        var samples = Distorted(9000, DistortionCurve.HardClip, 20).AsSpan(SampleRate / 10, SampleRate / 4);

        await Assert.That(Amplitude(samples, 3000) / Amplitude(samples, 9000)).IsLessThan(0.05);
    }
}
