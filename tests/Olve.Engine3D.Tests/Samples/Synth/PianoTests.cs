using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class PianoTests
{
    [Test]
    [Arguments(RackLayout.WideOctaves, 49, 29)]
    [Arguments(RackLayout.NarrowOctaves, 37, 22)]
    public async Task The_piano_runs_C_to_C_over_its_octaves_and_fills_the_keybed_right_of_the_cheek(int octaves, int keys, int whites)
    {
        var layout = new RackLayout(Presets.Classic(), octaves);
        var piano = layout.PianoKeys;

        await Assert.That(piano.Select(k => k.Semitone).Order()).IsEquivalentTo(Enumerable.Range(0, keys));
        await Assert.That(piano.Count(k => !k.IsBlack)).IsEqualTo(whites);
        await Assert.That(piano.Min(k => k.Bounds.Size.X)).IsGreaterThanOrEqualTo(RackLayout.MinWhiteKeyWidth * RackLayout.BlackKeyShare - 0.01f);
        await Assert.That(layout.Keys.Min.X).IsGreaterThan(layout.Cheek!.Bounds.Max.X);
        await Assert.That(layout.Keys.Max.X).IsEqualTo(layout.CaseWidth - RackLayout.Margin - RackLayout.RightCheek).Within(0.01f);
    }

    [Test]
    [Arguments(0, "A")]
    [Arguments(1, "W")]
    [Arguments(12, "K")]
    [Arguments(16, ";")]
    [Arguments(17, "'")]
    [Arguments(18, null)]
    [Arguments(36, null)]
    public async Task A_key_is_labelled_with_the_computer_key_that_plays_it(int semitone, string? label)
    {
        await Assert.That(KeyMap.Label(semitone)).IsEqualTo(label);
    }
}
