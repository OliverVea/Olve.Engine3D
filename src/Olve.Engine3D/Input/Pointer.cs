using Olve.Utilities.Ids;

namespace Olve.Engine3D.Input;

/// <summary>
/// Something that points at the screen: an absolute position axis in pixels, plus the buttons that act where it
/// points. The platform registers <see cref="Mouse"/>; a game can add pointers on a virtual axis it moves itself. A
/// touch screen's platform adds a <see cref="Touch"/> pointer for each finger while it touches
/// (<see cref="PlatformEvents.AddPointer"/>), so the pointers there are can change from frame to frame.
/// </summary>
/// <param name="Id">Identifies the pointer, e.g. for state kept per pointer.</param>
/// <param name="Position">Where the pointer is, in pixels from the top left of the content area.</param>
/// <param name="Primary">The button that selects and activates.</param>
/// <param name="Secondary">The button for secondary actions, or null when the pointer has none (a finger).</param>
public sealed record Pointer(Id<Pointer> Id, InputAxis Position, Button Primary, Button? Secondary)
{
    public InputAxis Position { get; } = Position.Kind == AxisKind.Absolute
        ? Position
        : throw new ArgumentException($"A pointer's position must be an absolute axis, not {Position}", nameof(Position));

    public static Pointer Mouse { get; } =
        new(Olve.Utilities.Ids.Id.FromName<Pointer>("Mouse"), MouseAxis.Position, MouseButton.Left, MouseButton.Right);

    /// <summary>
    /// A finger on a touch screen, by the number the platform gave the touch (a browser's pointer id), unique while it
    /// lasts. Its button is down while it touches.
    /// </summary>
    public static Pointer Touch(int touch) =>
        new(Olve.Utilities.Ids.Id.FromName<Pointer>($"Touch{touch}"), InputAxis.Touch(touch), Button.Touch(touch), null);
}
