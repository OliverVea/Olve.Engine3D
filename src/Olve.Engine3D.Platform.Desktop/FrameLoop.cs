namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// How frames are advanced.
/// </summary>
public enum FrameLoop
{
    /// <summary>The window's own loop, as fast as VSync allows.</summary>
    RealTime,

    /// <summary>Only on the <c>step</c> command (for tests and replays).</summary>
    Manual,
}
