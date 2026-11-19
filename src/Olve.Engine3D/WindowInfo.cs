namespace Olve.Engine3D;

/// <summary>
/// The window the game renders into.
/// </summary>
public interface IWindowInfo
{
    /// <summary>
    /// Size of the window's content area (excluding title bar and borders), in pixels. Fixed for the whole frame.
    /// </summary>
    Vector2<int> ContentAreaSize { get; }
}

/// <summary>
/// The window's state for the current frame, built from the frame's <see cref="ResizedEvent"/>s.
/// </summary>
public sealed class WindowInfo : IWindowInfo, IPlatformEventStage
{
    public int Order => PlatformEventStageOrder.Window;

    /// <summary>
    /// The last non-zero size the platform reported. A minimized window reports zero, which is ignored.
    /// </summary>
    public Vector2<int> ContentAreaSize { get; private set; }

    public IReadOnlyList<PlatformEvent> Apply(IReadOnlyList<PlatformEvent> events)
    {
        foreach (var platformEvent in events)
        {
            if (platformEvent is ResizedEvent { ContentAreaSize: { X: > 0, Y: > 0 } size })
            {
                ContentAreaSize = size;
            }
        }

        return events;
    }
}
