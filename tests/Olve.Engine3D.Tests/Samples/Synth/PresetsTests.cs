using Olve.Engine3D.Samples.Synth.Patching;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class PresetsTests
{
    // A cable as "MODULE JACK -> MODULE JACK", by the panels' titles
    private static string Describe(SynthPatch patch, Cable cable) =>
        $"{patch.Find(cable.From.Module)!.Definition.Title} {cable.From.Jack} -> {patch.Find(cable.To.Module)!.Definition.Title} {cable.To.Jack}";

    [Test]
    public async Task The_default_patch_plays_through_cables_alone_the_keys_into_the_oscillators_and_envelopes_and_the_voice_through_every_stage_to_both_speakers()
    {
        var patch = Presets.Classic();

        await Assert.That(patch.Cables.Select(c => Describe(patch, c))).IsEquivalentTo(
        [
            "KEYS Pitch -> OSCILLATORS VCO 1 Pitch",
            "KEYS Pitch -> OSCILLATORS VCO 2 Pitch",
            "KEYS Pitch -> OSCILLATORS VCO 3 Pitch",
            "KEYS Gate -> ENV Gate",
            "KEYS Gate -> ENV Gate",
            "OSCILLATORS Mix -> FILTER In",
            "FILTER Out -> VCA In",
            "VCA Out -> DRIVE/FOLD In",
            "DRIVE/FOLD Out -> PHASER In",
            "PHASER Out -> DELAY/CHO In",
            "DELAY/CHO Out -> REVERB In",
            "REVERB Out -> OUT In L",
            "REVERB Out -> OUT In R",
            "ENV Out -> VCA Level CV",
            "ENV Out -> FILTER Cutoff CV",
            "VCA Out -> SCOPE In",
            "LFO Out -> SCOPE In",
            "LFO Out -> PHASER Depth CV",
            "SEQ Pitch -> QUANTIZER In",
        ]);
        // The sequencer waits stopped, so the keys play the voice
        await Assert.That(patch.GetValue(Preset.Module(ModuleKind.Sequencer), Modules.Run)).IsEqualTo(0);
        // The first envelope opens the VCA and the second sweeps the filter
        await Assert.That(patch.CableInto(Preset.CvOf(Preset.Vca, Modules.Level))!.From.Module).IsEqualTo(Preset.Envelope);
        await Assert.That(patch.CableInto(Preset.CvOf(Preset.Filter, Modules.Cutoff))!.From.Module).IsEqualTo(Preset.Envelope2);
        // Nothing comes from the line input, so the page asks for none until it is patched
        await Assert.That(patch.Cables.Where(c => c.From.Module == Preset.Module(ModuleKind.LineIn))).IsEmpty();
    }
}
