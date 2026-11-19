using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Results;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>The preset's modules, by kind, and the nth of a kind it has several of.</summary>
internal static class Preset
{
    private static readonly SynthPatch Patch = Presets.Classic();

    public static ModuleId Module(ModuleKind kind, int index = 0) => Patch.Modules.Where(m => m.Kind == kind).ElementAt(index).Id;

    public static readonly ModuleId Keyboard = Module(ModuleKind.Keyboard);
    public static readonly ModuleId Oscillators = Module(ModuleKind.Oscillators);
    public static readonly ModuleId Mixer = Module(ModuleKind.Mixer);
    public static readonly ModuleId Lfo = Module(ModuleKind.Lfo);
    public static readonly ModuleId Filter = Module(ModuleKind.Filter);
    public static readonly ModuleId Envelope = Module(ModuleKind.Envelope);
    public static readonly ModuleId Envelope2 = Module(ModuleKind.Envelope, 1);
    public static readonly ModuleId Vca = Module(ModuleKind.Vca);
    public static readonly ModuleId Drive = Module(ModuleKind.Drive);
    public static readonly ModuleId Phaser = Module(ModuleKind.Phaser);
    public static readonly ModuleId Delay = Module(ModuleKind.Delay);
    public static readonly ModuleId Reverb = Module(ModuleKind.Reverb);
    public static readonly ModuleId Output = Module(ModuleKind.Output);

    public static JackRef CvOf(ModuleId module, string parameter) =>
        new(module, Patch.Find(module)!.Definition.FindParameter(parameter)!.CvJack);
}

public class SynthPatchTests
{
    private static JackRef Jack(ModuleId module, string jack) => new(module, jack);

    private static string Problem<T>(Result<T> result) =>
        result.TryPickProblems(out var problems) ? problems.First().FormattedMessage : "no problem";

    [Test]
    public async Task The_preset_is_panel_v2_with_two_saws_and_a_square_an_octave_down()
    {
        var patch = Presets.Classic();

        await Assert.That(patch.Modules.Select(m => m.Kind)).IsEquivalentTo(
        [
            ModuleKind.Keyboard, ModuleKind.Oscillators, ModuleKind.Lfo, ModuleKind.Lfo, ModuleKind.Random, ModuleKind.LineIn,
            ModuleKind.Filter, ModuleKind.Vca, ModuleKind.Envelope, ModuleKind.Envelope, ModuleKind.Drive, ModuleKind.Phaser,
            ModuleKind.Delay, ModuleKind.Reverb, ModuleKind.Mixer, ModuleKind.Output, ModuleKind.Slew, ModuleKind.Atten,
            ModuleKind.Atten, ModuleKind.Lights, ModuleKind.Scope, ModuleKind.Scope, ModuleKind.Sequencer, ModuleKind.Quantizer,
        ]);
        // Two saws a few cents apart and a square an octave down
        await Assert.That(Enumerable.Range(1, Modules.Vcos).Select(v => (
                patch.GetValue(Preset.Oscillators, Modules.Vco(v, Modules.Wave)),
                patch.GetValue(Preset.Oscillators, Modules.Vco(v, Modules.Tune)),
                patch.GetValue(Preset.Oscillators, Modules.Vco(v, Modules.Fine)))))
            .IsEquivalentTo([(2f, 0f, 0f), (2f, 0f, 7f), (1f, -12f, 0f)]);
    }

    public static IEnumerable<Func<(JackRef A, JackRef B, string Problem)>> InvalidCables() =>
    [
        () => (Jack(Preset.Oscillators, Modules.Mix), Jack(Preset.Lfo, Modules.Out), "A cable joins an output to an input, not two outputs"),
        () => (Jack(Preset.Filter, Modules.In), Jack(Preset.Vca, Modules.In), "A cable joins an output to an input, not two inputs"),
        () => (Jack(Preset.Lfo, Modules.Out), Jack(Preset.Envelope, Modules.Gate), "The GATE input takes only a gate"),
        () => (Jack(Preset.Lfo, Modules.Out), Jack(Preset.Lfo, Modules.Sync), "The SYNC input takes only a gate"),
        () => (Jack(Preset.Filter, Modules.Out), Preset.CvOf(Preset.Filter, Modules.Cutoff), "A cable can't loop back into a module it comes from"),
        // Through the cables: the VCA hears the filter, which hears the oscillators, and the reverb hears the whole chain
        () => (Jack(Preset.Vca, Modules.Out), Jack(Preset.Oscillators, Modules.Vco(1, Modules.Pitch)), "A cable can't loop back into a module it comes from"),
        () => (Jack(Preset.Reverb, Modules.Out), Jack(Preset.Filter, Modules.In), "A cable can't loop back into a module it comes from"),
        () => (Jack(Preset.Oscillators, "Sync"), Jack(Preset.Filter, Modules.In), "No jack Sync on module 2"),
        () => (Jack(Preset.Oscillators, Modules.Mix), Jack(new ModuleId(99), Modules.In), "No jack In on module 99"),
    ];

    [Test]
    [MethodDataSource(nameof(InvalidCables))]
    public async Task A_cable_that_cant_be_plugged_says_why_and_leaves_the_patch_alone(JackRef a, JackRef b, string problem)
    {
        var patch = Presets.Classic();
        var before = patch.Cables.ToList();

        await Assert.That(Problem(patch.Connect(a, b))).IsEqualTo(problem);
        await Assert.That(patch.Cables).IsEquivalentTo(before);
    }

    [Test]
    public async Task A_cable_dragged_from_an_input_runs_from_the_output()
    {
        var patch = Presets.Classic();

        var cutoff = Preset.CvOf(Preset.Filter, Modules.Cutoff);
        var change = patch.Connect(cutoff, Jack(Preset.Envelope, Modules.Out));

        await Assert.That(change).Succeeded();
        await Assert.That(patch.CableInto(cutoff)).IsEqualTo(new Cable(Jack(Preset.Envelope, Modules.Out), cutoff));
    }

    [Test]
    public async Task A_cable_into_a_plugged_input_replaces_the_one_there_while_an_output_feeds_many()
    {
        var patch = Presets.Classic();
        var cutoff = Preset.CvOf(Preset.Filter, Modules.Cutoff);
        var lfoToCutoff = new Cable(Jack(Preset.Lfo, Modules.Out), cutoff);
        var envelopeToCutoff = new Cable(Jack(Preset.Envelope, Modules.Out), cutoff);
        await Assert.That(patch.Connect(lfoToCutoff.From, lfoToCutoff.To)).Succeeded();
        await Assert.That(patch.Connect(envelopeToCutoff.From, Jack(Preset.Drive, Modules.Amount + " CV"))).Succeeded();

        var change = patch.Connect(envelopeToCutoff.From, envelopeToCutoff.To);

        await Assert.That(change.TryPickProblems(out _, out var value) ? null : value).IsEqualTo(new CableChange(envelopeToCutoff, lfoToCutoff));
        await Assert.That(patch.Cables).DoesNotContain(lfoToCutoff);
        await Assert.That(patch.Cables.Count(c => c.From == Jack(Preset.Envelope, Modules.Out))).IsEqualTo(3).Because("the VCA's level, the drive's amount and the cutoff");
    }

    [Test]
    public async Task Unplugging_removes_the_cable_once()
    {
        var patch = Presets.Classic();
        var cable = new Cable(Jack(Preset.Lfo, Modules.Out), Jack(Preset.Module(ModuleKind.Scope, 1), Modules.In));

        await Assert.That(patch.Disconnect(cable).IsSuccess).IsTrue();
        await Assert.That(patch.CableInto(cable.To)).IsNull();
        await Assert.That(patch.Disconnect(cable).WasNotFound).IsTrue();
    }

    [Test]
    [Arguments(Modules.Cutoff, 99_999f, 16_000f)]
    [Arguments(Modules.Cutoff, 1f, 40f)]
    [Arguments(Modules.Type, 0.6f, 1f)]
    [Arguments(Modules.Resonance, 7.25f, 7.25f)]
    public async Task A_knob_is_clamped_to_its_range_and_whole_steps(string parameter, float set, float expected)
    {
        var patch = Presets.Classic();

        await Assert.That(patch.SetValue(Preset.Filter, parameter, set)).Succeeded();
        await Assert.That(patch.GetValue(Preset.Filter, parameter)).IsEqualTo(expected);
    }

    // The knobs set only by hand, with no CV inputs: the sequencer's steps and gates and the quantizer's scale. The
    // keyboard's are all played by hand (below)
    private static IEnumerable<string> ByHand(ModuleKind kind) => kind switch
    {
        ModuleKind.Sequencer => Enumerable.Range(1, Modules.SequencerSteps).SelectMany(s => new[] { Modules.StepPitch(s), Modules.StepGate(s) }),
        ModuleKind.Quantizer => [Modules.Scale],
        _ => [],
    };

    public static IEnumerable<ModuleKind> PatchedKinds() => Enum.GetValues<ModuleKind>().Where(k => k != ModuleKind.Keyboard);

    [Test]
    [MethodDataSource(nameof(PatchedKinds))]
    public async Task Every_knob_but_those_set_by_hand_has_one_CV_input_of_its_own_and_no_two_jacks_share_a_name(ModuleKind kind)
    {
        var definition = Modules.Of(kind);
        var byHand = ByHand(kind).ToHashSet();

        await Assert.That(definition.Jacks.Select(j => j.Name)).HasDistinctItems();
        foreach (var parameter in definition.Parameters)
        {
            await Assert.That(definition.Jacks.Where(j => j.Knob == parameter.Name))
                .IsEquivalentTo(byHand.Contains(parameter.Name) ? Array.Empty<JackDefinition>() : [new JackDefinition($"{parameter.Name} CV", JackDirection.Input, SignalKind.Control, parameter.Name)]);
        }

        await Assert.That(definition.Jacks.Count(j => j.IsKnobInput)).IsEqualTo(definition.Parameters.Length - byHand.Count);
    }

    [Test]
    public async Task The_keyboards_knobs_are_played_by_hand_with_no_CV_inputs_and_no_two_of_its_jacks_share_a_name()
    {
        var definition = Modules.Of(ModuleKind.Keyboard);

        await Assert.That(definition.Jacks.Select(j => j.Name)).HasDistinctItems();
        await Assert.That(definition.Jacks.Where(j => j.IsKnobInput)).IsEmpty();
    }
}
