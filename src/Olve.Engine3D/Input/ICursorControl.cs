namespace Olve.Engine3D.Input;

/// <summary>
/// Hides and locks the mouse cursor. While locked it's also hidden, <see cref="MouseAxis.Position"/> stays at the
/// centre of the content area, and the mouse reports only <see cref="MouseAxis.Motion"/>.
/// </summary>
public interface ICursorControl
{
    bool IsLocked { get; }

    bool IsHidden { get; }

    void SetLocked(bool locked);

    /// <summary>Hides the cursor over the window, e.g. for a game that draws its own.</summary>
    void SetHidden(bool hidden);
}
