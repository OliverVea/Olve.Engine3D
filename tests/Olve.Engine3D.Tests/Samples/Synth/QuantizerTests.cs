using Olve.Engine3D.Samples.Synth.Patching;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class QuantizerTests
{
    private const int Chromatic = 0, Major = 1, Minor = 2, Pentatonic = 3, Blues = 4;

    // A pitch in, in semitones from middle C, and the note it comes out as
    public static IEnumerable<Func<(int Scale, float In, float Out)>> Notes() =>
    [
        () => (Chromatic, 0.4f, 0),
        () => (Chromatic, 0.6f, 1),
        () => (Chromatic, -0.6f, -1),
        () => (Major, 3.4f, 4),
        // Halfway between two notes, the lower
        () => (Major, 1, 0),
        () => (Major, 6, 5),
        // Across the octave's edge, either way
        () => (Major, 11.6f, 12),
        () => (Major, -1.2f, -1),
        () => (Major, 25, 24),
        () => (Minor, 4, 3),
        () => (Minor, 9.4f, 10),
        () => (Pentatonic, 5, 4),
        () => (Pentatonic, 10.6f, 12),
        () => (Blues, 2, 3),
        () => (Blues, 6.4f, 6),
    ];

    [Test]
    [MethodDataSource(nameof(Notes))]
    public async Task A_pitch_comes_out_as_the_nearest_note_of_the_scale(int scale, float semitonesIn, float semitonesOut)
    {
        await Assert.That(Quantizing.Quantize(semitonesIn / 12, scale) * 12).IsEqualTo(semitonesOut).Within(1e-4f);
    }

    [Test]
    public async Task The_scales_are_named_on_the_readout_and_their_knob_starts_on_major()
    {
        var scale = Modules.Quantizer.FindParameter(Modules.Scale)!;

        await Assert.That(Modules.Scales.Select(s => s.Name)).IsEquivalentTo(["CHROMATIC", "MAJOR", "MINOR", "PENTATONIC", "BLUES"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(scale.Choices).IsEquivalentTo(["CHROM", "MAJOR", "MINOR", "PENTA", "BLUES"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Knobs.Format(scale, scale.Default)).IsEqualTo("MAJOR");
    }

    [Test]
    [Arguments(Chromatic)]
    [Arguments(Pentatonic)]
    public async Task The_table_the_audio_looks_up_is_the_quantizer_across_its_span(int scale)
    {
        var table = Quantizing.Table(scale);

        await Assert.That(table.Length % 2).IsEqualTo(1);
        await Assert.That(table[table.Length / 2]).IsEqualTo(0);
        await Assert.That(table[0]).IsEqualTo(Quantizing.Quantize(-Quantizing.Span, scale));
        await Assert.That(table[^1]).IsEqualTo(Quantizing.Quantize(Quantizing.Span, scale));
        // A pitch a third of a semitone above C sits on C in the table too
        var third = table.Length / 2 + (int)(1f / 36 / Quantizing.Span * (table.Length - 1) / 2);
        await Assert.That(table[third]).IsEqualTo(0);
    }
}
