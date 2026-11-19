using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Button = Olve.Engine3D.Input.Button;
using ICursorControl = Olve.Engine3D.Input.ICursorControl;
using Key = Olve.Engine3D.Input.Key;
using MouseAxis = Olve.Engine3D.Input.MouseAxis;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// The desktop platform: a Silk.NET window drawn by <see cref="IDesktopGraphics"/>, reporting the window, keyboards and mice to
/// <see cref="PlatformEvents"/>. Locking the cursor puts the mice in raw mode.
/// </summary>
/// <remarks>
/// Devices can connect and disconnect while the game runs. A device that disconnects releases the buttons it held, so
/// none stay down. In the <see cref="FrameLoop.Manual"/> frame loop, commands drive the game, so no devices are
/// connected and the pointer starts at the centre of the window.
/// </remarks>
public sealed class DesktopPlatform(
    IWindow window,
    IDesktopGraphics graphics,
    PlatformEvents platformEvents,
    FrameLoop frameLoop,
    ILogger<DesktopPlatform> logger) : IPlatform, ICursorControl
{
    private readonly Dictionary<IInputDevice, ConnectedDevice> _devices = [];

    public bool IsLocked { get; private set; }

    public bool IsHidden { get; private set; }

    public void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        foreach (var mouse in _devices.Keys.OfType<IMouse>())
        {
            ApplyCursorMode(mouse);
        }
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        foreach (var (mouse, connected) in _devices.Where(device => device.Key is IMouse).Select(device => ((IMouse)device.Key, device.Value)))
        {
            // The cursor jumps when its mode changes; that isn't movement
            ApplyCursorMode(mouse);
            connected.LastMousePosition = mouse.Position;
            if (!locked)
            {
                platformEvents.AddAxis(MouseAxis.Position, ToPixels(mouse.Position));
            }
        }

        if (locked)
        {
            ReportCentre();
        }

        logger.LogDebug("Cursor {State}", locked ? "locked" : "unlocked");
    }

    private void ApplyCursorMode(IMouse mouse) =>
        mouse.Cursor.CursorMode = IsLocked
            ? mouse.Cursor.IsSupported(CursorMode.Raw) ? CursorMode.Raw : CursorMode.Disabled
            : IsHidden ? CursorMode.Hidden : CursorMode.Normal;

    private void ReportCentre() =>
        platformEvents.AddAxis(MouseAxis.Position, new Vector2(window.FramebufferSize.X / 2f, window.FramebufferSize.Y / 2f));

    public async Task<Result> InitializeAsync()
    {
        window.Initialize();

        if ((await graphics.InitializeAsync(window)).TryPickProblems(out var problems))
        {
            return problems;
        }

        window.FramebufferResize += size =>
        {
            platformEvents.AddResize(size.ToEngine());
            if (IsLocked)
            {
                ReportCentre();
            }
        };
        platformEvents.AddResize(window.FramebufferSize.ToEngine());

        if (frameLoop == FrameLoop.Manual)
        {
            ReportCentre();
            return Result.Success();
        }

        if (Result.Try<IInputContext, Exception>(() => window.CreateInput(), "Error while creating input context")
            .TryPickProblems(out problems, out var inputContext))
        {
            return problems;
        }

        ConnectInput(inputContext);

        return Result.Success();
    }

    private void ConnectInput(IInputContext inputContext)
    {
        inputContext.ConnectionChanged += (device, isConnected) =>
        {
            if (isConnected)
            {
                Connect(device);
            }
            else
            {
                Disconnect(device);
            }
        };

        foreach (var keyboard in inputContext.Keyboards)
        {
            Connect(keyboard);
        }

        foreach (var mouse in inputContext.Mice)
        {
            Connect(mouse);
        }
    }

    /// <summary>
    /// Starts reporting a keyboard or a mouse. Other devices have no engine mapping and are ignored.
    /// </summary>
    /// <remarks>
    /// A device that connects while already connected missed its disconnect, so it is disconnected first and its
    /// state starts fresh.
    /// </remarks>
    private void Connect(IInputDevice device)
    {
        logger.LogDebug("Connecting {Device}", Describe(device));

        if (!IsSupported(device))
        {
            logger.LogInformation("Ignoring {Device}: the engine has no mapping for it", Describe(device));
            return;
        }

        if (_devices.ContainsKey(device))
        {
            logger.LogWarning("{Device} connected while already connected; disconnecting it first", Describe(device));
            Disconnect(device);
        }

        var connected = new ConnectedDevice();
        switch (device)
        {
            case IKeyboard keyboard:
                ConnectKeyboard(keyboard, connected);
                break;
            case IMouse mouse:
                ConnectMouse(mouse, connected);
                break;
        }

        _devices.Add(device, connected);
        logger.LogInformation("Connected {Device}", Describe(device));
    }

    private void ConnectKeyboard(IKeyboard keyboard, ConnectedDevice connected)
    {
        Action<IKeyboard, Silk.NET.Input.Key, int> onKeyDown = (_, key, _) => OnKey(connected, key, isDown: true);
        Action<IKeyboard, Silk.NET.Input.Key, int> onKeyUp = (_, key, _) => OnKey(connected, key, isDown: false);

        keyboard.KeyDown += onKeyDown;
        keyboard.KeyUp += onKeyUp;

        connected.Unsubscribe = () =>
        {
            keyboard.KeyDown -= onKeyDown;
            keyboard.KeyUp -= onKeyUp;
        };
    }

    private void ConnectMouse(IMouse mouse, ConnectedDevice connected)
    {
        Action<IMouse, Silk.NET.Input.MouseButton> onMouseDown = (_, button) => OnButton(connected, button, isDown: true);
        Action<IMouse, Silk.NET.Input.MouseButton> onMouseUp = (_, button) => OnButton(connected, button, isDown: false);
        Action<IMouse, ScrollWheel> onScroll = (_, wheel) =>
            platformEvents.AddAxis(MouseAxis.Scroll, new Vector2(wheel.X, wheel.Y));
        connected.LastMousePosition = mouse.Position;
        Action<IMouse, System.Numerics.Vector2> onMouseMove = (_, position) =>
        {
            platformEvents.AddAxis(MouseAxis.Motion, ToPixels(position) - ToPixels(connected.LastMousePosition));
            connected.LastMousePosition = position;
            if (!IsLocked)
            {
                platformEvents.AddAxis(MouseAxis.Position, ToPixels(position));
            }
        };

        mouse.MouseDown += onMouseDown;
        mouse.MouseUp += onMouseUp;
        mouse.Scroll += onScroll;
        mouse.MouseMove += onMouseMove;

        connected.Unsubscribe = () =>
        {
            mouse.MouseDown -= onMouseDown;
            mouse.MouseUp -= onMouseUp;
            mouse.Scroll -= onScroll;
            mouse.MouseMove -= onMouseMove;
        };

        ApplyCursorMode(mouse);
        if (IsLocked)
        {
            ReportCentre();
        }
        else
        {
            platformEvents.AddAxis(MouseAxis.Position, ToPixels(mouse.Position));
        }
    }

    /// <summary>
    /// Stops reporting the device and releases the buttons it held, except those another device still holds.
    /// </summary>
    private void Disconnect(IInputDevice device)
    {
        logger.LogDebug("Disconnecting {Device}", Describe(device));

        if (!IsSupported(device))
        {
            logger.LogDebug("Ignoring {Device}: the engine has no mapping for it", Describe(device));
            return;
        }

        if (!_devices.Remove(device, out var connected))
        {
            logger.LogWarning("{Device} disconnected but wasn't connected; ignoring", Describe(device));
            return;
        }

        connected.Unsubscribe();

        var heldElsewhere = _devices.Values.SelectMany(other => other.Held).ToHashSet();
        var released = connected.Held.Where(button => !heldElsewhere.Contains(button)).ToArray();
        foreach (var button in released)
        {
            platformEvents.AddButtonUp(button);
        }

        logger.LogInformation("Disconnected {Device}, released {Count} held button(s): {Buttons}", Describe(device), released.Length, released);
    }

    private static bool IsSupported(IInputDevice device) => device is IKeyboard or IMouse;

    private static string Describe(IInputDevice device) =>
        $"{device.GetType().Name} '{device.Name}' (index {device.Index})";

    /// <summary>
    /// Silk.NET reports the mouse in screen coordinates; the engine works in pixels. They differ on scaled displays.
    /// </summary>
    private Vector2 ToPixels(System.Numerics.Vector2 position)
    {
        var sizeInScreenCoordinates = window.Size;
        var sizeInPixels = window.FramebufferSize;
        if (sizeInScreenCoordinates.X <= 0 || sizeInScreenCoordinates.Y <= 0)
        {
            return new Vector2(position.X, position.Y);
        }

        return new Vector2(
            position.X * sizeInPixels.X / sizeInScreenCoordinates.X,
            position.Y * sizeInPixels.Y / sizeInScreenCoordinates.Y);
    }

    private void OnKey(ConnectedDevice device, Silk.NET.Input.Key key, bool isDown)
    {
        if (key.ToEngine() is var engineKey and not Key.Unknown)
        {
            Report(device, engineKey, isDown);
        }
    }

    private void OnButton(ConnectedDevice device, Silk.NET.Input.MouseButton button, bool isDown)
    {
        if (button.ToEngine() is { } engineButton)
        {
            Report(device, engineButton, isDown);
        }
    }

    private void Report(ConnectedDevice device, Button button, bool isDown)
    {
        if (isDown)
        {
            device.Held.Add(button);
            platformEvents.AddButtonDown(button);
        }
        else
        {
            device.Held.Remove(button);
            platformEvents.AddButtonUp(button);
        }
    }

    /// <summary>A device the platform reports, with the buttons it holds.</summary>
    private sealed class ConnectedDevice
    {
        public HashSet<Button> Held { get; } = [];

        public Action Unsubscribe { get; set; } = () => { };

        /// <summary>A mouse's position at its last move, to report how far it moved.</summary>
        public System.Numerics.Vector2 LastMousePosition { get; set; }
    }
}
