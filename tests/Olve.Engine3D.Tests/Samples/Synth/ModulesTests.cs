using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>Panel v2's modules as docs/design/synth-panel-v2.md names them: what the panel prints, and the jacks the audio and the drawing rely on.</summary>
public class ModulesTests
{
    private const JackDirection In = JackDirection.Input, Out = JackDirection.Output;
    private const SignalKind Sound = SignalKind.Audio, Cv = SignalKind.Control, Gate = SignalKind.Gate;

    public static IEnumerable<Func<(ModuleKind Kind, string Heading, string[] Knobs, (string, JackDirection, SignalKind)[] Jacks)>> Panels() =>
    [
        () => (ModuleKind.Oscillators, "OSCILLATORS",
            ["WAVE", "TUNE", "FINE", "LEVEL", "WAVE", "TUNE", "FINE", "LEVEL", "WAVE", "TUNE", "FINE", "LEVEL"],
            [("VCO 1 PITCH", In, Cv), ("VCO 2 PITCH", In, Cv), ("VCO 3 PITCH", In, Cv),
             ("VCO 1 OUT", Out, Sound), ("VCO 2 OUT", Out, Sound), ("VCO 3 OUT", Out, Sound), ("MIX", Out, Sound)]),
        () => (ModuleKind.Lfo, "LFO", ["WAVE", "RATE", "DEPTH"], [("SYNC", In, Gate), ("OUT", Out, Cv)]),
        () => (ModuleKind.Random, "RANDOM", ["RATE"], [("CLOCK", In, Gate), ("NOISE", Out, Sound), ("S&H", Out, Cv), ("SMOOTH", Out, Cv)]),
        () => (ModuleKind.LineIn, "LINE IN · FOLLOW", ["GAIN", "ATK", "REL"], [("ENV", Out, Cv), ("AUDIO", Out, Sound)]),
        () => (ModuleKind.Filter, "FILTER", ["LP/HP", "CUTOFF", "RES", "MOD"], [("IN", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Vca, "VCA · RING", ["LEVEL", "RING"], [("IN", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Envelope, "ENV", ["ATK", "DEC", "SUS", "REL"], [("GATE", In, Gate), ("OUT", Out, Cv)]),
        () => (ModuleKind.Drive, "DRIVE/FOLD", ["AMOUNT", "FOLD", "MIX"], [("IN", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Phaser, "PHASER", ["RATE", "DEPTH", "FDBK"], [("IN", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Delay, "DELAY/CHO", ["TIME", "FDBK", "MOD", "MIX"], [("IN", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Reverb, "REVERB", ["SIZE", "TONE", "MIX"], [("IN", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Mixer, "MIXER", ["LVL 1", "LVL 2", "LVL 3", "LVL 4"],
            [("IN 1", In, Sound), ("IN 2", In, Sound), ("IN 3", In, Sound), ("IN 4", In, Sound), ("OUT", Out, Sound)]),
        () => (ModuleKind.Output, "OUT", ["VOLUME", "PAN"], [("IN L", In, Sound), ("IN R", In, Sound)]),
        () => (ModuleKind.Slew, "SLEW · GLIDE", ["RISE", "FALL", "SHAPE"], [("IN", In, Cv), ("OUT", Out, Cv)]),
        () => (ModuleKind.Atten, "ATTEN", ["SCALE", "OFFSET"], [("IN", In, Cv), ("OUT", Out, Cv)]),
        () => (ModuleKind.Sequencer, "SEQUENCER",
            ["RUN", "TEMPO", "LEN", .. Enumerable.Range(1, 16).Select(s => $"{s}"), .. Enumerable.Range(1, 16).Select(s => $"G{s}")],
            [("CLOCK IN", In, Gate), ("RESET", In, Gate), ("PITCH", Out, Cv), ("GATE", Out, Gate), ("CLOCK", Out, Gate)]),
        () => (ModuleKind.Quantizer, "QUANTIZER", ["SCALE"], [("IN", In, Cv), ("OUT", Out, Cv)]),
    ];

    [Test]
    [MethodDataSource(nameof(Panels))]
    public async Task A_module_prints_its_heading_and_knobs_and_has_these_jacks_besides_its_knobs_CV_inputs(
        ModuleKind kind, string heading, string[] knobs, (string, JackDirection, SignalKind)[] jacks)
    {
        var definition = Modules.Of(kind);

        await Assert.That(definition.Heading).IsEqualTo(heading);
        await Assert.That(definition.Parameters.Select(p => p.Caption)).IsEquivalentTo(knobs, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(definition.Jacks.Where(j => !j.IsKnobInput).Select(j => (j.Name.ToUpperInvariant(), j.Direction, j.Signal)))
            .IsEquivalentTo(jacks, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // SLEW and ATTEN pass on what they hear: their OUT carries audio while audio goes in, else a control voltage
    public static IEnumerable<Func<(string Into, ModuleKind From, string Jack, SignalKind Out)>> Followers() =>
    [
        () => (Modules.In, ModuleKind.Oscillators, Modules.Mix, SignalKind.Audio),
        () => (Modules.In, ModuleKind.Random, Modules.Noise, SignalKind.Audio),
        () => (Modules.In, ModuleKind.Lfo, Modules.Out, SignalKind.Control),
        () => (Modules.In, ModuleKind.Keyboard, Modules.Gate, SignalKind.Control),
        () => ("", ModuleKind.Oscillators, Modules.Mix, SignalKind.Control),
    ];

    [Test]
    [MethodDataSource(nameof(Followers))]
    public async Task SLEW_and_ATTEN_carry_audio_out_only_while_audio_goes_in(string into, ModuleKind from, string jack, SignalKind expected)
    {
        foreach (var kind in new[] { ModuleKind.Slew, ModuleKind.Atten })
        {
            var patch = new SynthPatch();
            var source = patch.Add(from).Id;
            var follower = patch.Add(kind).Id;
            if (into.Length > 0)
            {
                await Assert.That(patch.Connect(new JackRef(source, jack), new JackRef(follower, into))).Succeeded();
            }

            // Followed through a second one, too
            var next = patch.Add(kind).Id;
            await Assert.That(patch.Connect(new JackRef(follower, Modules.Out), new JackRef(next, Modules.In))).Succeeded();

            await Assert.That(patch.SignalOf(new JackRef(follower, Modules.Out))).IsEqualTo(expected).Because($"{kind}'s OUT");
            await Assert.That(patch.SignalOf(new JackRef(next, Modules.Out))).IsEqualTo(expected).Because($"a second {kind}'s OUT");
        }
    }
}
