using System.Text.RegularExpressions;

namespace Olve.Engine3D.Samples.Synth.Playing;

public enum LineInState
{
    /// <summary>Not used yet: the page hasn't asked for an input.</summary>
    Idle,

    /// <summary>Waiting for the user to allow an input.</summary>
    Asking,

    Live,

    /// <summary>The user or the browser refused the input.</summary>
    Denied,

    /// <summary>The device has no audio input, or the browser offers none.</summary>
    NoInput,
}

/// <summary>A line input as the audio has it: its state, and while live the input it plays, of how many.</summary>
public sealed record LineInStatus(LineInState State, string Device = "", int Index = 0, int Count = 0);

/// <summary>What the line input's face says.</summary>
public static partial class LineInput
{
    /// <summary>The picker's text, in capitals and at most <paramref name="width"/> characters: the input's name while live.</summary>
    public static string Text(LineInStatus status, int width)
    {
        var text = status.State switch
        {
            LineInState.Idle => "TAP TO USE",
            LineInState.Asking => "ASKING",
            LineInState.Denied => "DENIED",
            LineInState.NoInput => "NO INPUT",
            _ => Name(status.Device),
        };
        return text.Length > width ? text[..width].TrimEnd() : text;
    }

    /// <summary>The display's first line: which of the device's inputs plays, while one does.</summary>
    public static string Caption(LineInStatus status) =>
        status is { State: LineInState.Live, Count: > 0 } ? $"IN {status.Index + 1}/{status.Count}" : "IN --";

    // A device's label, without the USB ids browsers add, as "SCARLETT 2I2 USB" for "Scarlett 2i2 USB (1235:8210)"
    private static string Name(string label)
    {
        var name = UsbIds().Replace(label, "").Trim().ToUpperInvariant();
        return name.Length > 0 ? name : "INPUT";
    }

    [GeneratedRegex(@"\s*\([0-9a-fA-F]{4}:[0-9a-fA-F]{4}\)")]
    private static partial Regex UsbIds();
}
