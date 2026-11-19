namespace Olve.Engine3D.Platform.Web;

public sealed record WebHostOptions
{
    public required string AppName { get; init; }

    public string CanvasSelector { get; init; } = "#canvas";

    /// <summary>Multisample anti-aliasing samples; 0 turns it off.</summary>
    public int Samples { get; init; }

    /// <summary>
    /// The most canvas pixels per CSS pixel; the page draws at the device's pixel ratio up to this,
    /// so phones with dense screens don't render more pixels than they can fill.
    /// </summary>
    public double MaxPixelRatio { get; init; } = 2;
}
