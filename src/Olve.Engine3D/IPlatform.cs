namespace Olve.Engine3D;

/// <summary>
/// The platform the game runs on (window, graphics context, input devices).
/// </summary>
public interface IPlatform
{
    /// <summary>
    /// Creates the graphics context and starts reporting the window and input devices to <see cref="PlatformEvents"/>,
    /// beginning with the window's current size. Called once, before the first frame.
    /// </summary>
    Task<Result> InitializeAsync();
}
