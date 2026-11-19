using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class SequencerTests
{
    // The preset with two sequencers after its modules, so the preset's ids stay as they are
    private static readonly ModuleId First = new(Presets.Classic().Modules.Count + 1), Second = new(First.Value + 1);

    private static SynthPatch Patch()
    {
        var patch = Presets.Classic();
        patch.Add(ModuleKind.Sequencer);
        patch.Add(ModuleKind.Sequencer);
        return patch;
    }

    private static JackRef Jack(ModuleId module, string jack) => new(module, jack);

    [Test]
    public async Task The_sequencer_has_sixteen_steps_each_a_pitch_within_an_octave_either_way_in_semitones_and_a_gate()
    {
        var definition = Modules.Sequencer;
        var steps = Enumerable.Range(1, 16).ToList();

        await Assert.That(steps.Select(s => definition.FindParameter(Modules.StepPitch(s))!).All(p =>
            p is { Min: -12, Max: 12, Default: 0, Scale: ParameterScale.Stepped, Unit: "ST" })).IsTrue();
        await Assert.That(steps.Select(s => definition.FindParameter(Modules.StepGate(s))!).All(p =>
            p is { Min: 0, Max: 1, Default: 1, Scale: ParameterScale.Stepped })).IsTrue();
        // Its knobs' names and its jacks are in ModulesTests' table
    }

    [Test]
    [Arguments(13f, 12f)]
    [Arguments(-30f, -12f)]
    [Arguments(2.4f, 2f)]
    [Arguments(-6.6f, -7f)]
    public async Task A_steps_pitch_snaps_to_whole_semitones_within_an_octave_either_way(float set, float expected)
    {
        var patch = Patch();

        await Assert.That(patch.SetValue(First, Modules.StepPitch(5), set)).Succeeded();
        await Assert.That(patch.GetValue(First, Modules.StepPitch(5))).IsEqualTo(expected);
    }

    public static IEnumerable<Func<(JackRef From, JackRef To, string Problem)>> Cables() =>
    [
        // Its gate and clock open envelopes, and its pitch and gate are signals like any other
        () => (Jack(First, Modules.Gate), Jack(Preset.Envelope, Modules.Gate), ""),
        () => (Jack(First, Modules.Clock), Jack(Preset.Envelope, Modules.Gate), ""),
        () => (Jack(First, Modules.Pitch), Jack(Preset.Oscillators, Modules.Vco(1, Modules.Pitch)), ""),
        () => (Jack(First, Modules.Gate), Preset.CvOf(Preset.Vca, Modules.Level), ""),
        // Its clock input follows any gate: another sequencer's clock or the keys
        () => (Jack(First, Modules.Clock), Jack(Second, Modules.ClockIn), ""),
        () => (Jack(Preset.Keyboard, Modules.Gate), Jack(First, Modules.ClockIn), ""),
        () => (Jack(Preset.Lfo, Modules.Out), Jack(First, Modules.ClockIn), "The CLOCK IN input takes only a gate"),
        () => (Jack(First, Modules.Pitch), Jack(Second, Modules.ClockIn), "The CLOCK IN input takes only a gate"),
        () => (Jack(First, Modules.Clock), Jack(First, Modules.ClockIn), "A cable can't loop back into a module it comes from"),
    ];

    [Test]
    [MethodDataSource(nameof(Cables))]
    public async Task What_the_sequencer_can_be_patched_to(JackRef from, JackRef to, string problem)
    {
        var plan = Patch().Plan(from, to);

        await Assert.That(plan.TryPickProblems(out var problems) ? problems.First().FormattedMessage : "").IsEqualTo(problem);
    }

    [Test]
    public async Task A_clock_chain_cant_close_into_a_loop()
    {
        var patch = Patch();
        await Assert.That(patch.Connect(Jack(First, Modules.Clock), Jack(Second, Modules.ClockIn))).Succeeded();

        await Assert.That(patch.Plan(Jack(Second, Modules.Clock), Jack(First, Modules.ClockIn)).TryPickProblems(out var problems)
            ? problems.First().FormattedMessage
            : "").IsEqualTo("A cable can't loop back into a module it comes from");
    }

    [Test]
    public async Task The_sequencer_plays_one_voice_so_an_envelope_only_it_opens_is_shared()
    {
        var audio = new SynthRackTests.RecordingAudio();
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        await Assert.That(rack.Load(Patch())).Succeeded();
        await Assert.That(audio.Calls).Contains($"add {First} Sequencer x1");
        var vco = Jack(Preset.Oscillators, Modules.Vco(1, Modules.Pitch));
        audio.Calls.Clear();

        await Assert.That(rack.Connect(Jack(First, Modules.Gate), Jack(Preset.Envelope, Modules.Gate))).Succeeded();
        // The other two oscillators still follow the keys, so the bank keeps its voices
        await Assert.That(rack.Connect(Jack(First, Modules.Pitch), vco)).Succeeded();

        await Assert.That(audio.Log).IsEqualTo(string.Join("\n",
            $"disconnect {new Cable(Jack(Preset.Keyboard, Modules.Gate), Jack(Preset.Envelope, Modules.Gate))}",
            $"voices {Preset.Envelope} 1",
            $"connect {new Cable(Jack(First, Modules.Gate), Jack(Preset.Envelope, Modules.Gate))}",
            $"disconnect {new Cable(Jack(Preset.Keyboard, Modules.Pitch), vco)}",
            $"connect {new Cable(Jack(First, Modules.Pitch), vco)}"));
    }
}
