using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Audio;

public class PatchRendererTests
{
    private const int SampleRate = 48_000;

    private static Patch Tone(Waveform waveform, float frequency, params PatchParameter[] parameters) => new(
        [new PatchNode.Oscillator("tone", waveform, frequency)], [], [.. parameters], "tone");

    private static float[] Render(Patch patch, double seconds, IReadOnlyDictionary<string, float>? parameters = null)
    {
        var result = PatchRenderer.Render(patch, SampleRate, TimeSpan.FromSeconds(seconds), parameters);
        return result.TryPickProblems(out var problems, out var buffer)
            ? throw new InvalidOperationException(string.Join("; ", problems.Select(p => p.ToDebugString())))
            : buffer.GetChannel(0).ToArray();
    }

    private static int RisingZeroCrossings(float[] samples)
    {
        var count = 0;
        for (var i = 1; i < samples.Length; i++)
        {
            if (samples[i - 1] < 0 && samples[i] >= 0)
            {
                count++;
            }
        }

        return count;
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

    [Test]
    [Arguments(Waveform.Sine)]
    [Arguments(Waveform.Square)]
    [Arguments(Waveform.Sawtooth)]
    [Arguments(Waveform.Triangle)]
    public async Task An_Oscillator_Plays_Its_Frequency(Waveform waveform)
    {
        var samples = Render(Tone(waveform, 440), 1);

        await Assert.That(samples.Length).IsEqualTo(SampleRate);
        await Assert.That(RisingZeroCrossings(samples)).IsBetween(438, 441);
        await Assert.That(samples.Max()).IsLessThanOrEqualTo(1.1f);
    }

    [Test]
    public async Task A_Parameter_Replaces_The_Value_It_Names()
    {
        var patch = Tone(Waveform.Sine, 440, new PatchParameter(Id.New<PatchInput>(), "Pitch", "tone", PatchNode.Oscillator.FrequencyParameter));

        var samples = Render(patch, 1, new Dictionary<string, float> { ["Pitch"] = 880 });

        await Assert.That(RisingZeroCrossings(samples)).IsBetween(878, 881);
    }

    [Test]
    public async Task An_Envelope_Shapes_The_Volume_And_Rings_Out_Over_Its_Release()
    {
        var patch = new Patch(
            [
                new PatchNode.Oscillator("tone", Waveform.Sine, 440),
                new PatchNode.Envelope("shape", TimeSpan.FromMilliseconds(10), TimeSpan.Zero, 1, TimeSpan.FromMilliseconds(100)),
                new PatchNode.Gain("volume", 0),
            ],
            [
                new PatchConnection("tone", "volume"),
                new PatchConnection("shape", "volume", PatchNode.Gain.AmountParameter),
            ],
            [],
            "volume");

        var samples = Render(patch, 0.5);

        // Held for 0.5 s, then the 100 ms release
        await Assert.That(samples.Length).IsEqualTo(SampleRate * 6 / 10);
        await Assert.That(samples[0]).IsEqualTo(0f);
        await Assert.That(Rms(samples.AsSpan(SampleRate / 10, SampleRate / 10))).IsBetween(0.69, 0.72);
        await Assert.That(samples[^1]).IsBetween(-0.001f, 0.001f);
    }

    [Test]
    public async Task A_Lowpass_Filter_Removes_What_Is_Above_Its_Cutoff()
    {
        Patch Through(FilterType type) => new(
            [
                new PatchNode.Oscillator("tone", Waveform.Sine, 8000),
                new PatchNode.Filter("filter", type, 500, 0),
            ],
            [new PatchConnection("tone", "filter")],
            [],
            "filter");

        var lowpassed = Render(Through(FilterType.Lowpass), 0.5);
        var highpassed = Render(Through(FilterType.Highpass), 0.5);

        await Assert.That(Rms(lowpassed.AsSpan(SampleRate / 10))).IsLessThan(0.01);
        await Assert.That(Rms(highpassed.AsSpan(SampleRate / 10))).IsGreaterThan(0.6);
    }

    [Test]
    public async Task A_Connection_Into_A_Parameter_Adds_Its_Signal()
    {
        // A gain of 0 plus a 1 Hz square (+1, then -1) lets the tone through, unchanged, for the first half second
        var patch = new Patch(
            [
                new PatchNode.Oscillator("tone", Waveform.Sine, 1000),
                new PatchNode.Oscillator("lfo", Waveform.Square, 1),
                new PatchNode.Gain("volume", 0),
            ],
            [
                new PatchConnection("tone", "volume"),
                new PatchConnection("lfo", "volume", PatchNode.Gain.AmountParameter),
            ],
            [],
            "volume");

        var samples = Render(patch, 1);

        await Assert.That(Rms(samples.AsSpan(SampleRate / 10, SampleRate / 4))).IsBetween(0.69, 0.72);
    }

    [Test]
    public async Task The_Same_Patch_Renders_The_Same_Samples()
    {
        var patch = new Patch([new PatchNode.Noise("noise", 42)], [], [], "noise");

        await Assert.That(Render(patch, 0.1)).IsEquivalentTo(Render(patch, 0.1));
        await Assert.That(Rms(Render(patch, 0.1))).IsBetween(0.5, 0.65);
    }

    [Test]
    public async Task A_Patch_That_Cannot_Be_Built_Fails_To_Render()
    {
        Patch[] broken =
        [
            new([new PatchNode.Gain("a", 1)], [], [], "missing"),
            new([new PatchNode.Gain("a", 1), new PatchNode.Gain("a", 1)], [], [], "a"),
            new([new PatchNode.Gain("a", 1)], [new PatchConnection("nowhere", "a")], [], "a"),
            new([new PatchNode.Gain("a", 1), new PatchNode.Oscillator("b", Waveform.Sine, 1)], [new PatchConnection("a", "b")], [], "b"),
            new([new PatchNode.Gain("a", 1), new PatchNode.Gain("b", 1)], [new PatchConnection("a", "b", "Volume")], [], "b"),
            new([new PatchNode.Gain("a", 1), new PatchNode.Gain("b", 1)], [new PatchConnection("a", "b"), new PatchConnection("b", "a")], [], "b"),
        ];

        foreach (var patch in broken)
        {
            await Assert.That(PatchRenderer.Render(patch, SampleRate, TimeSpan.FromSeconds(0.1))).Failed();
        }

        await Assert.That(PatchRenderer.Render(Tone(Waveform.Sine, 440), SampleRate, TimeSpan.FromSeconds(0.1),
            new Dictionary<string, float> { ["Unknown"] = 1 })).Failed();
    }

    [Test]
    public async Task A_Wav_File_Has_The_Samples_After_Its_Header()
    {
        var buffer = new SampleBuffer();
        buffer.Reset(2, 3);
        buffer.GetChannel(0)[1] = 0.5f;
        buffer.GetChannel(1)[2] = -0.25f;

        using var stream = new MemoryStream();
        WavEncoder.Write(stream, buffer, SampleRate);
        var bytes = stream.ToArray();

        await Assert.That(bytes.Length).IsEqualTo(44 + 3 * 2 * 4);
        await Assert.That(System.Text.Encoding.ASCII.GetString(bytes, 0, 4)).IsEqualTo("RIFF");
        await Assert.That(BitConverter.ToInt32(bytes, 24)).IsEqualTo(SampleRate);
        await Assert.That(BitConverter.ToSingle(bytes, 44 + 2 * 4)).IsEqualTo(0.5f);
        await Assert.That(BitConverter.ToSingle(bytes, 44 + 5 * 4)).IsEqualTo(-0.25f);
    }
}
