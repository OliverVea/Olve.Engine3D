using Olve.Engine3D.Samples.Synth.Patching;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class KnobsTests
{
    private static ParameterDefinition Parameter(ModuleDefinition module, string name) =>
        module.FindParameter(name) ?? throw new InvalidOperationException($"No {name}");

    [Test]
    public async Task An_exponential_knob_gives_equal_ratios_for_equal_turns()
    {
        var cutoff = Parameter(Modules.Filter, Modules.Cutoff);

        await Assert.That(Knobs.FromTurn(cutoff, 0)).IsEqualTo(40f).Within(0.01f);
        await Assert.That(Knobs.FromTurn(cutoff, 0.5f)).IsEqualTo(800f).Within(0.5f);
        await Assert.That(Knobs.FromTurn(cutoff, 1)).IsEqualTo(16_000f).Within(1f);
        await Assert.That(Knobs.ToTurn(cutoff, 800)).IsEqualTo(0.5f).Within(0.0001f);
    }

    public static IEnumerable<Func<(ParameterDefinition Parameter, float Value, string Text)>> Readouts() =>
    [
        () => (Parameter(Modules.Oscillators, Modules.Vco(1, Modules.Wave)), 2, "SAW"),
        () => (Parameter(Modules.Oscillators, Modules.Vco(2, Modules.Tune)), 7, "+7 ST"),
        () => (Parameter(Modules.Oscillators, Modules.Vco(3, Modules.Tune)), -12, "-12 ST"),
        () => (Parameter(Modules.Output, Modules.Pan), -0.5f, "-0.50"),
        () => (Parameter(Modules.Atten, Modules.Scale), -1, "-1.00"),
        () => (Parameter(Modules.Slew, Modules.Rise), 0.25f, "0.25 S"),
        () => (Parameter(Modules.Filter, Modules.Cutoff), 1234, "1.23K HZ"),
        () => (Parameter(Modules.Filter, Modules.Cutoff), 12_345, "12.3K HZ"),
        () => (Parameter(Modules.Envelope, Modules.Attack), 0.01f, "0.01 S"),
        () => (Parameter(Modules.Envelope, Modules.Sustain), 0.6f, "0.60"),
        () => (Parameter(Modules.Sequencer, Modules.Run), 0, "STOP"),
        () => (Parameter(Modules.Sequencer, Modules.Run), 1, "RUN"),
        () => (Parameter(Modules.Sequencer, Modules.Tempo), 120, "120 BPM"),
        () => (Parameter(Modules.Sequencer, Modules.Length), 16, "16"),
        () => (Parameter(Modules.Sequencer, Modules.StepPitch(3)), 7, "+7 ST"),
        () => (Parameter(Modules.Sequencer, Modules.StepGate(3)), 0, "OFF"),
        () => (Parameter(Modules.Sequencer, Modules.StepGate(3)), 1, "ON"),
    ];

    [Test]
    [MethodDataSource(nameof(Readouts))]
    public async Task A_knob_reads_its_value_with_its_unit(ParameterDefinition parameter, float value, string text) =>
        await Assert.That(Knobs.Format(parameter, value)).IsEqualTo(text);

    public static IEnumerable<Func<(ParameterDefinition Parameter, float Value, float Cv, float Expected)>> Modulations() =>
    [
        // No signal leaves the knob where it is
        () => (Parameter(Modules.Delay, Modules.Feedback), 0.4f, 0, 0.4f),
        // A unit of signal turns it half its sweep, either way, and stops at the ends
        () => (Parameter(Modules.Delay, Modules.Feedback), 0.2f, 0.5f, 0.2f + 0.95f / 4),
        () => (Parameter(Modules.Delay, Modules.Feedback), 0.4f, -0.2f, 0.4f - 0.095f),
        () => (Parameter(Modules.Delay, Modules.Feedback), 0.8f, 1, 0.95f),
        () => (Parameter(Modules.Delay, Modules.Feedback), 0.2f, -3, 0),
        // An exponential knob moves by ratios: half a turn of 0.05 Hz to 20 Hz is 20 times
        () => (Parameter(Modules.Lfo, Modules.Rate), 0.5f, 1, 10),
        () => (Parameter(Modules.Lfo, Modules.Rate), 0.5f, -0.5f, 0.5f / MathF.Sqrt(20)),
        // A stepped knob glides between its steps
        () => (Parameter(Modules.Oscillators, Modules.Vco(1, Modules.Wave)), 1, 0.25f, 1.375f),
        // The VCA's level takes its CV one for one, stopping at the knob's ends here (the audio goes past them)
        () => (Parameter(Modules.Vca, Modules.Level), 0.2f, 0.5f, 0.7f),
        // The filter's cutoff takes its CV as octaves, by its MOD knob, not as a turn
        () => (Parameter(Modules.Filter, Modules.Cutoff), 800, 1, 800),
    ];

    [Test]
    [MethodDataSource(nameof(Modulations))]
    public async Task A_knobs_CV_adds_to_its_turn_and_the_audio_looks_the_same_value_up(ParameterDefinition parameter, float value, float cv, float expected)
    {
        await Assert.That(Knobs.Modulate(parameter, value, cv)).IsEqualTo(expected).Within(expected * 0.0001f + 0.0001f);

        // The audio's table, looked up by the turn plus the signal, between its two nearest points
        var curve = Knobs.Curve(parameter);
        var at = float.Clamp((2 * Knobs.ToTurn(parameter, value) - 1 + cv * Knobs.CvGain(parameter) + 1) / 2, 0, 1) * (curve.Length - 1);
        var below = (int)MathF.Floor(at);
        var looked = below >= curve.Length - 1 ? curve[^1] : float.Lerp(curve[below], curve[below + 1], at - below);
        await Assert.That(curve.Length % 2).IsEqualTo(1);
        await Assert.That(looked).IsEqualTo(expected).Within(expected * 0.002f + 0.0001f);
    }
}
