using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Input;

/// <summary>
/// The cursor control for a platform that can't hide or lock its cursor: locking only moves
/// <see cref="MouseAxis.Position"/> to the centre.
/// </summary>
public sealed class CentredCursorControl(PlatformEvents platformEvents, IWindowInfo windowInfo, ILogger<CentredCursorControl> logger)
    : ICursorControl
{
    public bool IsLocked { get; private set; }

    public bool IsHidden { get; private set; }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        logger.LogDebug("Cursor {State} (this platform can't hide it)", locked ? "locked" : "unlocked");

        if (locked)
        {
            var size = windowInfo.ContentAreaSize;
            platformEvents.AddAxis(MouseAxis.Position, new Vector2(size.X / 2f, size.Y / 2f));
        }
    }

    public void SetHidden(bool hidden) => IsHidden = hidden;
}
