using Olve.Engine3D.Samples.Synth.Playing;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class LineInputTests
{
    public static IEnumerable<Func<(LineInStatus Status, string Text, string Caption)>> Faces() =>
    [
        () => (new LineInStatus(LineInState.Idle), "TAP TO USE", "IN --"),
        () => (new LineInStatus(LineInState.Asking), "ASKING", "IN --"),
        () => (new LineInStatus(LineInState.Denied), "DENIED", "IN --"),
        () => (new LineInStatus(LineInState.NoInput), "NO INPUT", "IN --"),
        () => (new LineInStatus(LineInState.Live, "Scarlett 2i2 USB (1235:8210)", 1, 3), "SCARLETT 2I", "IN 2/3"),
        () => (new LineInStatus(LineInState.Live, "Mic", 0, 1), "MIC", "IN 1/1"),
        // Before the user allows an input the browser gives no labels
        () => (new LineInStatus(LineInState.Live, "", 0, 2), "INPUT", "IN 1/2"),
    ];

    [Test]
    [MethodDataSource(nameof(Faces))]
    public async Task The_picker_names_the_input_playing_or_says_why_none_does(LineInStatus status, string text, string caption)
    {
        await Assert.That(LineInput.Text(status, 11)).IsEqualTo(text);
        await Assert.That(LineInput.Caption(status)).IsEqualTo(caption);
    }
}
