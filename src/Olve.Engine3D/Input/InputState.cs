using Olve.Utilities.Ids;
namespace Olve.Engine3D.Input;

/// <summary>
/// The frame's input: button and axis states, the <see cref="Pointers"/> there are, and the frame's
/// <see cref="Events"/> in order. Built once at the start of each frame from the <see cref="PlatformEvents"/>, so it
/// doesn't change during a frame except through <see cref="Consume"/>.
/// </summary>
/// <remarks>
/// Scene services read input in <c>Input()</c>, in scene and priority order. A service that handles a button
/// consumes it (<see cref="TryConsumePress"/>, <see cref="Consume"/>), which hides it from the services after it for
/// the rest of the frame. Held keys read for continuous movement, and modifiers, aren't consumed.
/// </remarks>
public sealed class InputState : IPlatformEventStage
{
    private readonly IWindowInfo _windowInfo;

    private readonly HashSet<Button> _down = [];
    private readonly HashSet<Button> _pressed = [];
    private readonly HashSet<Button> _released = [];
    private readonly HashSet<Button> _consumed = [];

    // A pointer becomes the main pointer when one of its buttons goes down, or when it moves this far: a device from
    // where it was, a virtual axis from where the main pointer is
    private const float PointerSwitchDistance = 4f;

    private readonly Dictionary<InputAxis, Vector2> _values = [];
    private readonly Dictionary<Id<Pointer>, Vector2> _switchAnchors = [];
    private readonly Dictionary<InputAxis, Vector2> _previousValues = [];

    private readonly List<Pointer> _pointers;
    private readonly List<Pointer> _leaving = [];

    /// <param name="pointers">The pointers there always are (the platform's and the game's); a device's is <see cref="MainPointer"/> at first.</param>
    public InputState(IWindowInfo windowInfo, IEnumerable<Pointer> pointers)
    {
        _windowInfo = windowInfo;
        _pointers = pointers.ToList();
        MainPointer = FallbackPointer();

        if (_pointers.Count == 0)
        {
            throw new ArgumentException("The platform must register at least one pointer", nameof(pointers));
        }
    }

    public int Order => PlatformEventStageOrder.Input;

    /// <summary>
    /// The pointers there are: those there always are, and those the platform added and hasn't removed, such as a
    /// finger touching. A removed pointer stays for the rest of the frame it was removed in, so its last release is
    /// read like any other.
    /// </summary>
    public IReadOnlyList<Pointer> Pointers => _pointers;

    /// <summary>
    /// The pointer that world interaction (raycasts, tools) follows: the one used last, at first the first a device moves.
    /// </summary>
    public Pointer MainPointer { get; private set; }

    /// <summary>The frame's events, oldest first. Includes events for consumed buttons.</summary>
    public IReadOnlyList<PlatformEvent> Events { get; private set; } = [];

    public bool Shift => IsDown(Key.ShiftLeft) || IsDown(Key.ShiftRight);
    public bool Control => IsDown(Key.ControlLeft) || IsDown(Key.ControlRight);
    public bool Alt => IsDown(Key.AltLeft) || IsDown(Key.AltRight);

    /// <summary>Whether the button is held at the end of the frame's events.</summary>
    public bool IsDown(Button button) => !_consumed.Contains(button) && _down.Contains(button);

    /// <summary>Whether the button went down during the frame, even if it also went up again.</summary>
    public bool WasPressed(Button button) => !_consumed.Contains(button) && _pressed.Contains(button);

    /// <summary>Whether the button went up during the frame, even if it also went down again.</summary>
    public bool WasReleased(Button button) => !_consumed.Contains(button) && _released.Contains(button);

    /// <summary>
    /// Marks the button as handled: <see cref="IsDown"/>, <see cref="WasPressed"/> and <see cref="WasReleased"/>
    /// report false for it until the frame ends.
    /// </summary>
    public void Consume(Button button) => _consumed.Add(button);

    /// <summary>
    /// Consumes the button if it was pressed during the frame and nothing has consumed it yet.
    /// </summary>
    /// <returns>Whether the button was pressed, so the caller should handle it.</returns>
    public bool TryConsumePress(Button button)
    {
        if (!WasPressed(button))
        {
            return false;
        }

        Consume(button);
        return true;
    }

    public bool IsConsumed(Button button) => _consumed.Contains(button);

    /// <summary>
    /// An absolute axis's value at the end of the frame's events, or a relative axis's total change during the frame.
    /// Zero for an axis that hasn't reported anything.
    /// </summary>
    public Vector2 GetValue(InputAxis axis) => _values.GetValueOrDefault(axis);

    /// <summary>
    /// An absolute axis's value at the end of the previous frame; zero for a relative axis.
    /// </summary>
    public Vector2 GetPreviousValue(InputAxis axis) =>
        axis.Kind == AxisKind.Absolute ? _previousValues.GetValueOrDefault(axis) : Vector2.Zero;

    /// <summary>How much the axis changed during the frame.</summary>
    public Vector2 GetDelta(InputAxis axis) => GetValue(axis) - GetPreviousValue(axis);

    /// <summary>The pointer's position in pixels, from the top left of the content area.</summary>
    public Vector2 GetPosition(Pointer pointer) => GetValue(pointer.Position);

    /// <summary>The pointer's position in normalized coordinates (see <see cref="ScreenPosition"/>).</summary>
    public Vector2 GetNormalizedPosition(Pointer pointer)
    {
        var size = _windowInfo.ContentAreaSize;
        return size.X > 0 && size.Y > 0
            ? ScreenPosition.ToNormalized(GetPosition(pointer), size)
            : Vector2.Zero;
    }

    public IReadOnlyList<PlatformEvent> Apply(IReadOnlyList<PlatformEvent> events)
    {
        _pressed.Clear();
        _released.Clear();
        _consumed.Clear();

        _previousValues.Clear();
        foreach (var (axis, value) in _values)
        {
            if (axis.Kind == AxisKind.Absolute)
            {
                _previousValues[axis] = value;
            }
        }

        foreach (var axis in _values.Keys.Where(axis => axis.Kind == AxisKind.Relative).ToArray())
        {
            _values.Remove(axis);
        }

        RemoveLeavingPointers();

        foreach (var platformEvent in events)
        {
            switch (platformEvent)
            {
                case ButtonDownEvent down:
                    _pressed.Add(down.Button);
                    _down.Add(down.Button);
                    break;
                case ButtonUpEvent up:
                    _released.Add(up.Button);
                    _down.Remove(up.Button);
                    break;
                case AxisEvent { Axis.Kind: AxisKind.Absolute } absolute:
                    _values[absolute.Axis] = absolute.Value;
                    break;
                case AxisEvent relative:
                    _values[relative.Axis] = _values.GetValueOrDefault(relative.Axis) + relative.Value;
                    break;
                case PointerAddedEvent { Pointer: var added }:
                    _leaving.Remove(added);
                    if (!_pointers.Contains(added))
                    {
                        _pointers.Add(added);
                    }

                    break;
                case PointerRemovedEvent { Pointer: var removed } when _pointers.Contains(removed) && !_leaving.Contains(removed):
                    _leaving.Add(removed);
                    break;
            }
        }

        UpdateMainPointer(events);

        Events = events;
        return events;
    }

    // The pointers removed last frame go, with their state; the main pointer falls back if it was one of them
    private void RemoveLeavingPointers()
    {
        foreach (var pointer in _leaving)
        {
            _pointers.Remove(pointer);
            _values.Remove(pointer.Position);
            _previousValues.Remove(pointer.Position);
            _switchAnchors.Remove(pointer.Id);
            _down.Remove(pointer.Primary);
            if (pointer.Secondary is { } secondary)
            {
                _down.Remove(secondary);
            }
        }

        if (_leaving.Contains(MainPointer))
        {
            MainPointer = FallbackPointer();
        }

        _leaving.Clear();
    }

    // A device's pointer, whatever order the host and the game registered theirs in
    private Pointer FallbackPointer() =>
        _pointers.FirstOrDefault(pointer => pointer.Position.Device != InputDevice.Virtual) ?? _pointers.FirstOrDefault()!;

    /// <summary>Makes the pointer used last the main one.</summary>
    private void UpdateMainPointer(IReadOnlyList<PlatformEvent> events)
    {
        foreach (var platformEvent in events)
        {
            foreach (var pointer in Pointers)
            {
                if (pointer == MainPointer || !IsUse(pointer, platformEvent))
                {
                    continue;
                }

                MainPointer = pointer;
                foreach (var other in Pointers)
                {
                    _switchAnchors[other.Id] = GetPosition(other);
                }
            }
        }
    }

    private bool IsUse(Pointer pointer, PlatformEvent platformEvent) => platformEvent switch
    {
        // A pointer that has never been placed can't take over: it would act at the corner
        ButtonDownEvent down => (down.Button == pointer.Primary || down.Button == pointer.Secondary)
            && _values.ContainsKey(pointer.Position),
        // A virtual pointer written to where the main pointer is, or was last frame (a write lands a frame late, while
        // the mouse may have moved on), follows it without taking over
        AxisEvent { Axis.Device: InputDevice.Virtual } move when move.Axis == pointer.Position =>
            Vector2.Distance(GetPosition(MainPointer), move.Value) > PointerSwitchDistance
            && Vector2.Distance(GetPreviousValue(MainPointer.Position), move.Value) > PointerSwitchDistance,
        AxisEvent move when move.Axis == pointer.Position =>
            !_switchAnchors.TryGetValue(pointer.Id, out var anchor)
            || Vector2.Distance(anchor, move.Value) > PointerSwitchDistance,
        _ => false,
    };
}
