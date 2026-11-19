namespace Olve.Engine3D.Input;

/// <summary>
/// The kind of device a <see cref="Button"/> or an <see cref="InputAxis"/> is on.
/// </summary>
public enum InputDevice
{
    Keyboard,
    Mouse,

    /// <summary>A touch screen: each finger touching has its own button and position.</summary>
    Touch,

    /// <summary>No device: values a service writes, such as a pointer driven by keys (see <see cref="InputAxis.Virtual"/>).</summary>
    Virtual,
}

/// <summary>
/// A button on an input device: a keyboard key, a mouse button or a finger on a touch screen. <see cref="Key"/> and <see cref="MouseButton"/>
/// convert to it implicitly.
/// </summary>
public readonly record struct Button
{
    private Button(InputDevice device, int code)
    {
        Device = device;
        Code = code;
    }

    public InputDevice Device { get; }

    /// <summary>The button's code on its device, e.g. the <see cref="Key"/> value.</summary>
    public int Code { get; }

    public static Button Of(Key key) => new(InputDevice.Keyboard, (int)key);

    public static Button Of(MouseButton mouseButton) => new(InputDevice.Mouse, (int)mouseButton);

    /// <summary>A finger on a touch screen, down while it touches (see <see cref="Pointer.Touch"/>).</summary>
    public static Button Touch(int touch) => new(InputDevice.Touch, touch);

    public static implicit operator Button(Key key) => Of(key);

    public static implicit operator Button(MouseButton mouseButton) => Of(mouseButton);

    public override string ToString() => Device switch
    {
        InputDevice.Keyboard => $"Key.{(Key)Code}",
        InputDevice.Mouse => $"Mouse.{(MouseButton)Code}",
        InputDevice.Touch => $"Touch.{Code}",
        _ => $"{Device}.{Code}",
    };
}
