using Olve.Engine3D.Input;

namespace Olve.Engine3D;

/// <summary>
/// Collects <see cref="PlatformEvent"/>s between frames, in the order they happened. The platform adds what the
/// devices and the window report, the audio output adds its format, and test commands add simulated input the same way. The engine takes all of them
/// at the start of each frame.
/// </summary>
public sealed class PlatformEvents
{
    private readonly Lock _lock = new();
    private List<PlatformEvent> _pending = [];

    public void AddButtonDown(Button button) => Add(new ButtonDownEvent(button));

    public void AddButtonUp(Button button) => Add(new ButtonUpEvent(button));

    /// <param name="value">The new value of an absolute axis, or the change of a relative one.</param>
    public void AddAxis(InputAxis axis, Vector2 value) => Add(new AxisEvent(axis, value));

    public void AddPointer(Pointer pointer) => Add(new PointerAddedEvent(pointer));

    public void RemovePointer(Pointer pointer) => Add(new PointerRemovedEvent(pointer));

    /// <param name="contentAreaSize">In pixels.</param>
    public void AddResize(Vector2<int> contentAreaSize) => Add(new ResizedEvent(contentAreaSize));


    /// <summary>
    /// Returns the events added since the last call, oldest first, and starts collecting anew.
    /// </summary>
    public IReadOnlyList<PlatformEvent> Take()
    {
        lock (_lock)
        {
            var taken = _pending;
            _pending = [];
            return taken;
        }
    }

    private void Add(PlatformEvent platformEvent)
    {
        lock (_lock)
        {
            _pending.Add(platformEvent);
        }
    }
}
