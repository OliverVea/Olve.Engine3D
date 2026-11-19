namespace Olve.Engine3D.Input;

/// <summary>
/// How an <see cref="InputAxis"/>'s reported values combine within a frame.
/// </summary>
public enum AxisKind
{
    /// <summary>Each value replaces the last, e.g. a position in pixels.</summary>
    Absolute,

    /// <summary>Each value is a change that adds up within the frame, e.g. scroll wheel movement.</summary>
    Relative,
}

/// <summary>
/// A mouse axis. Platforms translate their native mouse reports into these.
/// </summary>
public enum MouseAxis
{
    /// <summary>The cursor position in pixels, from the top left of the content area (absolute).</summary>
    Position,

    /// <summary>Scroll wheel movement; positive Y scrolls up (relative).</summary>
    Scroll,

    /// <summary>How far the mouse moved, in pixels, even while the cursor is locked (relative).</summary>
    Motion,
}

/// <summary>
/// A two-dimensional input on a device: the counterpart of <see cref="Button"/> for values such as a position or
/// scroll movement. <see cref="MouseAxis"/> converts to it implicitly.
/// </summary>
public readonly record struct InputAxis
{
    private InputAxis(InputDevice device, int code, AxisKind kind)
    {
        Device = device;
        Code = code;
        Kind = kind;
    }

    public InputDevice Device { get; }

    /// <summary>The axis's code on its device, e.g. the <see cref="MouseAxis"/> value.</summary>
    public int Code { get; }

    public AxisKind Kind { get; }

    /// <summary>Where a finger on a touch screen is, in pixels from the top left of the content area (absolute).</summary>
    public static InputAxis Touch(int touch) => new(InputDevice.Touch, touch, AxisKind.Absolute);

    public static InputAxis Of(MouseAxis axis) => new(InputDevice.Mouse, (int)axis, axis switch
    {
        MouseAxis.Scroll or MouseAxis.Motion => AxisKind.Relative,
        _ => AxisKind.Absolute,
    });

    /// <summary>
    /// An absolute axis no device reports; a service writes it through <see cref="PlatformEvents.AddAxis"/>. Writing a
    /// pointer's virtual axis away from the main pointer makes it the main pointer; writing it to the main pointer's
    /// position lets it follow without taking over.
    /// </summary>
    public static InputAxis Virtual(int code) => new(InputDevice.Virtual, code, AxisKind.Absolute);

    public static implicit operator InputAxis(MouseAxis axis) => Of(axis);

    public override string ToString() => Device switch
    {
        InputDevice.Mouse => $"Mouse.{(MouseAxis)Code}",
        InputDevice.Touch => $"Touch.{Code}",
        _ => $"{Device}.{Code}",
    };
}
