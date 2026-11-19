using System.Numerics;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Input;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Samples.Synth.Rack;

/// <summary>A drag in progress with one pointer.</summary>
public abstract record RackDrag
{
    private RackDrag()
    {
    }

    /// <summary>A cable held by one end; the other end stays in <see cref="Fixed"/>. <see cref="Unplugged"/> is the cable it was before it was pulled out.</summary>
    public sealed record Cable(JackRef Fixed, Patching.Cable? Unplugged = null) : RackDrag
    {
        /// <summary>The jack it was picked up from: the plugged end pulled out, or the jack it starts at.</summary>
        public JackRef Grabbed => Unplugged?.To ?? Fixed;
    }

    /// <summary>A knob turned by dragging up and down, from the turn it had when grabbed.</summary>
    public sealed record Knob(KnobLayout Layout, float StartTurn, float StartY, bool Moved) : RackDrag;

    /// <summary>A piano key held down with the pointer.</summary>
    public sealed record PianoKey(int Semitone) : RackDrag;

    /// <summary>A thumb wheel rolled by dragging along it, from the value it had when grabbed.</summary>
    public sealed record Wheel(WheelLayout Layout, float StartValue, float StartY) : RackDrag;
}

/// <summary>A pointer dragging on the rack: where it is, in design units, what is under it and what it drags.</summary>
public sealed record RackPointer(Id<Pointer> Pointer, Vector2 Position, RackHit? Hovered, RackDrag Drag);

/// <summary>How the hint line reads: the usual help, a call to start the audio, or why something didn't work.</summary>
public enum HintTone
{
    Help,
    Start,
    Problem,
}

/// <summary>The line of help over the rack.</summary>
public sealed record RackHint(string Text, HintTone Tone);

/// <summary>
/// Plays and patches the rack: the computer keyboard plays notes, and the pointers turn knobs, play the on-screen
/// piano, pick the line input and drag cables between jacks (dragging a plugged input's cable away unplugs it). Every pointer drags on its
/// own, so on a touch screen each finger does. A click (or a tap) on a jack instead picks a cable up: its loose end
/// follows the pointer until a click on a jack plugs it, a click anywhere else drops it unplugged, and Escape or a
/// right-click puts it back as it was. Pointers reach the rack through <paramref name="views"/>, or the rack laid flat
/// on the screen without it. The keybed's wheels roll as they are dragged along, and the pitch wheel springs back to its
/// middle when let go; its octave buttons and the line input's step buttons act when pressed.
/// <para>
/// A host can also play notes as their input arrives, between frames (<see cref="KeyArrived"/>, <see cref="PressArrived"/>,
/// <see cref="ReleaseArrived"/>), so a note doesn't wait for the next frame; when the frame then sees that input, it
/// leaves the note alone.
/// </para>
/// </summary>
public class RackInputService(
    SynthRack rack,
    InputState input,
    IWindowInfo window,
    DeltaTimeService time,
    ILogger<RackInputService> logger,
    IRackViews? views = null,
    SynthOptions? options = null) : ISceneService
{
    /// <summary>How far, in design units, a knob is dragged to turn it all the way.</summary>
    private const float FullTurnDrag = 160;

    public RackLayout Layout { get; private set; } = null!;

    private readonly Dictionary<Id<Pointer>, RackDrag> _drags = [];
    private readonly Dictionary<Id<Pointer>, Vector2> _positions = [];
    private Vector2 _hoveredAt = new(float.NaN);

    // Presses and releases already played as they arrived, for the frame to skip
    private readonly HashSet<Key> _keysPressed = [];
    private readonly HashSet<Key> _keysReleased = [];
    private readonly HashSet<Id<Pointer>> _pointersPressed = [];
    // The wheels springing back, by the wheel: how far from the middle they are, and how fast they move
    private readonly Dictionary<WheelLayout, (float Turn, float Speed)> _springs = [];

    public IRackView View { get; private set; } = new RackView(1, Vector2.Zero);

    /// <summary>What is under the main pointer.</summary>
    public RackHit? Hovered { get; private set; }

    /// <summary>The pointers dragging something.</summary>
    public IReadOnlyList<RackPointer> Drags { get; private set; } = [];

    /// <summary>Why the last cable couldn't be plugged, until the next press.</summary>
    public string? Problem { get; private set; }

    /// <summary>The cable picked up by a click, its loose end where the pointer that carries it was last on the rack.</summary>
    public RackPointer? Carried { get; private set; }

    public RackHint Hint => Problem is { } problem
        ? new RackHint(problem, HintTone.Problem)
        : !rack.Audio.IsRunning
            ? new RackHint(StartHint, HintTone.Start)
            : new RackHint(Carried is null ? PatchHint : CarryHint, HintTone.Help);

    public const string StartHint = "TAP OR CLICK TO START AUDIO, THEN PLAY THE KEYS";
    public const string PatchHint = "PLAY A-', Z/X FOR OCTAVES. CLICK OR DRAG JACK TO JACK TO PATCH";
    public const string CarryHint = "CLICK A GLOWING JACK TO PLUG, ELSEWHERE TO UNPLUG, ESC TO PUT BACK";

    public Result Load()
    {
        if (rack.Load(Presets.Classic()).TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to load the preset patch");
        }

        Layout = new RackLayout(rack.Patch, (options ?? new SynthOptions()).Octaves);
        return Result.Success();
    }

    public Result Input()
    {
        var screen = window.ContentAreaSize;
        var size = new Vector2(int.Max(1, screen.X), int.Max(1, screen.Y));
        View = views is null ? RackView.Fit(Layout.Size, size) : views.Fit(Layout, size);
        // The layout stays put, so what is under the pointer changes only when the point of the layout it is over does
        var hovered = View.ToDesign(input.GetPosition(input.MainPointer));
        if (hovered != _hoveredAt && !(float.IsNaN(hovered.X) && float.IsNaN(_hoveredAt.X)))
        {
            _hoveredAt = hovered;
            Hovered = Layout.HitTest(hovered);
        }

        PlayKeys();
        Spring((float)time.RawDeltaTime.TotalSeconds);
        if (Carried is not null && (input.WasPressed(Key.Escape) || input.Pointers.Any(p => p.Secondary is { } secondary && input.WasPressed(secondary))))
        {
            PutBack();
        }

        var dragging = new List<RackPointer>();
        foreach (var pointer in input.Pointers)
        {
            if (Follow(pointer) is { } dragged)
            {
                dragging.Add(dragged);
            }
        }

        if (Carried is { } carried)
        {
            dragging.Add(carried);
        }

        Drags = dragging;
        // This frame's input holds everything that arrived before it
        _keysPressed.Clear();
        _keysReleased.Clear();
        _pointersPressed.Clear();
        return Result.Success();
    }

    // Presses, drags and releases with one pointer; the drag it is left with, if any
    private RackPointer? Follow(Pointer pointer)
    {
        var position = View.ToDesign(input.GetPosition(pointer));
        var drag = _drags.GetValueOrDefault(pointer.Id);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
        {
            // Pointing past everything the view can reach: a drag holds where it was
            return drag is null ? null : new RackPointer(pointer.Id, _positions.GetValueOrDefault(pointer.Id), null, drag);
        }

        _positions[pointer.Id] = position;
        var hit = Layout.HitTest(position);
        if (Carried is { } carried && carried.Pointer == pointer.Id)
        {
            // The loose end follows the pointer that carries it
            Carried = carried with { Position = position, Hovered = hit };
        }

        if (input.WasPressed(pointer.Primary) && !_pointersPressed.Remove(pointer.Id))
        {
            if (drag is not null)
            {
                Release(drag, hit, pointer.Id);
            }

            if (Carried is not null)
            {
                Drop(hit);
                drag = null;
            }
            else
            {
                drag = Press(hit, position);
            }
        }

        if (drag is not null)
        {
            drag = Move(drag, position, hit);
            if (input.WasReleased(pointer.Primary) || !input.IsDown(pointer.Primary))
            {
                Release(drag, hit, pointer.Id);
                drag = null;
            }
        }

        if (drag is null)
        {
            _drags.Remove(pointer.Id);
            return null;
        }

        _drags[pointer.Id] = drag;
        return new RackPointer(pointer.Id, position, hit, drag);
    }

    private void PlayKeys()
    {
        for (var semitone = 0; semitone < KeyMap.Notes.Count; semitone++)
        {
            var key = KeyMap.Notes[semitone];
            var pressed = input.WasPressed(key) && !_keysPressed.Remove(key);
            var released = input.WasReleased(key) && !_keysReleased.Remove(key);
            var down = input.IsDown(key);
            // A key can go down and up within one frame, or up and down again
            if (released && down)
            {
                rack.Release(semitone);
            }

            if (pressed)
            {
                rack.Press(semitone);
            }

            if (released && !down)
            {
                rack.Release(semitone);
            }
        }

        var octaveStep = (input.WasPressed(KeyMap.OctaveUp) ? 1 : 0) - (input.WasPressed(KeyMap.OctaveDown) ? 1 : 0);
        if (octaveStep != 0)
        {
            foreach (var keyboard in rack.Patch.Modules.Where(m => m.Kind == ModuleKind.Keyboard))
            {
                SetValue(keyboard.Id, Modules.Octave, rack.Patch.GetValue(keyboard.Id, Modules.Octave) + octaveStep);
            }
        }
    }

    /// <summary>A key went down or up just now: a note key plays or stops at once. Every key event should come here.</summary>
    public void KeyArrived(Key key, bool isDown)
    {
        var semitone = KeyMap.SemitoneOf(key);
        if (semitone is null)
        {
            return;
        }

        if (isDown)
        {
            rack.Press(semitone.Value);
            _keysPressed.Add(key);
        }
        else
        {
            rack.Release(semitone.Value);
            _keysReleased.Add(key);
        }
    }

    /// <summary>
    /// The pointer's primary button went down just now at <paramref name="screen"/>, in pixels: a piano key under it plays
    /// at once, as seen through the last frame's view. Anything else waits for the frame.
    /// </summary>
    public void PressArrived(Pointer pointer, Vector2 screen)
    {
        // A press while a cable is carried drops it, wherever it lands, so it waits for the frame
        if (Carried is not null || _drags.ContainsKey(pointer.Id) || Layout.HitTest(View.ToDesign(screen)) is not RackHit.PianoKey { Layout.Semitone: var semitone })
        {
            return;
        }

        // A press clears the last cable's problem, as one the frame sees does
        Problem = null;
        rack.Press(semitone);
        _drags[pointer.Id] = new RackDrag.PianoKey(semitone);
        _pointersPressed.Add(pointer.Id);
    }

    /// <summary>The pointer's primary button went up just now: the piano key it holds stops at once.</summary>
    public void ReleaseArrived(Pointer pointer)
    {
        if (_drags.GetValueOrDefault(pointer.Id) is RackDrag.PianoKey held)
        {
            rack.Release(held.Semitone);
            _drags.Remove(pointer.Id);
        }
    }

    private RackDrag? Press(RackHit? hit, Vector2 position)
    {
        Problem = null;
        switch (hit)
        {
            case RackHit.Jack { Layout: var jack } when jack.Definition.Direction == JackDirection.Input
                                                        && rack.Patch.CableInto(jack.Jack) is { } plugged:
                // Take the plugged end out: the cable now hangs from its output
                Unplug(plugged);
                return new RackDrag.Cable(plugged.From, plugged);
            case RackHit.Jack { Layout: var jack }:
                return new RackDrag.Cable(jack.Jack);
            case RackHit.Knob { Layout: var knob }:
                var turn = Knobs.ToTurn(knob.Parameter, rack.Patch.GetValue(knob.Module, knob.Parameter.Name));
                return new RackDrag.Knob(knob, turn, position.Y, Moved: false);
            case RackHit.PianoKey { Layout: var key }:
                rack.Press(key.Semitone);
                return new RackDrag.PianoKey(key.Semitone);
            case RackHit.Picker { Panel: var panel }:
                rack.StepLineIn(panel.Module.Id);
                return null;
            case RackHit.Button { Layout: var button }:
                PressButton(button);
                return null;
            case RackHit.Wheel { Layout: var wheel }:
                _springs.Remove(wheel);
                return new RackDrag.Wheel(wheel, rack.Patch.GetValue(wheel.Module, wheel.Parameter.Name), position.Y);
            default:
                return null;
        }
    }

    private RackDrag Move(RackDrag drag, Vector2 position, RackHit? hit)
    {
        switch (drag)
        {
            case RackDrag.Knob knob:
                var dragged = knob.StartY - position.Y;
                if (!knob.Moved && MathF.Abs(dragged) < 3)
                {
                    return knob;
                }

                var parameter = knob.Layout.Parameter;
                // A fader's cap follows the pointer along its slot; a knob turns all the way over a longer drag
                SetValue(knob.Layout.Module, parameter.Name, Knobs.FromTurn(parameter, knob.StartTurn + dragged / (knob.Layout.Slot?.Size.Y ?? FullTurnDrag)));
                return knob with { Moved = true };
            case RackDrag.Wheel { Layout: var rolling } wheel:
                var range = rolling.Parameter.Max - rolling.Parameter.Min;
                // Dragged along the wheel's length, it rolls from one end to the other
                SetValue(rolling.Module, rolling.Parameter.Name, wheel.StartValue + (wheel.StartY - position.Y) / rolling.Bounds.Size.Y * range);
                return wheel;
            case RackDrag.PianoKey held when hit is RackHit.PianoKey { Layout.Semitone: var semitone } && semitone != held.Semitone:
                // Sliding along the keys plays each one passed over
                rack.Press(semitone);
                rack.Release(held.Semitone);
                return new RackDrag.PianoKey(semitone);
            default:
                return drag;
        }
    }

    private void Release(RackDrag drag, RackHit? hit, Id<Pointer> pointer)
    {
        switch (drag)
        {
            case RackDrag.Cable cable when hit is RackHit.Jack { Layout: var grabbed } && grabbed.Jack == cable.Grabbed:
                // Let go where it was picked up: a click, so the cable stays in hand
                Carried = new RackPointer(pointer, grabbed.Centre, hit, cable);
                logger.LogDebug("Picked up a cable from {Jack}", cable.Fixed);
                break;
            case RackDrag.Cable cable when hit is RackHit.Jack { Layout: var target }:
                if (rack.Connect(cable.Fixed, target.Jack).TryPickProblems(out var problems))
                {
                    Problem = problems.First().FormattedMessage.ToUpperInvariant();
                    logger.LogInformation("Didn't plug a cable: {Problem}", problems.First().FormattedMessage);
                }

                break;
            case RackDrag.Knob { Moved: false, Layout: { Parameter.Scale: ParameterScale.Stepped } knob }:
                // A click steps a switch-like knob on, round to its first position after its last
                var parameter = knob.Parameter;
                var next = rack.Patch.GetValue(knob.Module, parameter.Name) + 1;
                SetValue(knob.Module, parameter.Name, next > parameter.Max ? parameter.Min : next);
                break;
            case RackDrag.PianoKey held:
                rack.Release(held.Semitone);
                break;
            case RackDrag.Wheel { Layout: { Springs: true } wheel }:
                _springs[wheel] = (rack.Patch.GetValue(wheel.Module, wheel.Parameter.Name), 0);
                break;
        }
    }

    private void PressButton(ButtonLayout button)
    {
        switch (button.Action)
        {
            case ButtonAction.OctaveDown or ButtonAction.OctaveUp:
                var step = button.Action == ButtonAction.OctaveUp ? 1 : -1;
                SetValue(button.Module, Modules.Octave, rack.Patch.GetValue(button.Module, Modules.Octave) + step);
                break;
            case ButtonAction.PreviousInput or ButtonAction.NextInput:
                rack.StepLineIn(button.Module, button.Action == ButtonAction.NextInput ? 1 : -1);
                break;
        }
    }

    // Moves each wheel let go toward its middle, by the spring
    private void Spring(float seconds)
    {
        foreach (var (wheel, (turn, speed)) in _springs.ToList())
        {
            var next = WheelSpring.Step(turn, speed, seconds);
            SetValue(wheel.Module, wheel.Parameter.Name, next.Turn);
            if (next == (0, 0))
            {
                _springs.Remove(wheel);
            }
            else
            {
                _springs[wheel] = next;
            }
        }
    }

    // A click while a cable is carried: on a jack it plugs the cable there, anywhere else it drops the cable unplugged
    private void Drop(RackHit? hit)
    {
        Problem = null;
        var carried = (RackDrag.Cable)Carried!.Drag;
        if (hit is not RackHit.Jack { Layout: var target })
        {
            Carried = null;
            logger.LogInformation("Dropped a cable from {Jack}", carried.Fixed);
            return;
        }

        if (target.Jack == carried.Fixed)
        {
            PutBack();
            return;
        }

        if (rack.Connect(carried.Fixed, target.Jack).TryPickProblems(out var problems))
        {
            // Still in hand, to try another jack
            Problem = problems.First().FormattedMessage.ToUpperInvariant();
            logger.LogInformation("Didn't plug a cable: {Problem}", problems.First().FormattedMessage);
            return;
        }

        Carried = null;
    }

    // Puts the carried cable back as it was before it was picked up
    private void PutBack()
    {
        var carried = (RackDrag.Cable)Carried!.Drag;
        Carried = null;
        Problem = null;
        if (carried.Unplugged is { } unplugged && rack.Connect(unplugged.From, unplugged.To).TryPickProblems(out var problems))
        {
            logger.LogError("Failed to put a cable back: {Problems}", string.Join("; ", problems.Select(p => p.ToBriefString())));
        }
    }

    private void Unplug(Cable cable)
    {
        if (rack.Disconnect(cable).TryPickProblems(out var problems))
        {
            logger.LogError("Failed to unplug a cable: {Problems}", string.Join("; ", problems.Select(p => p.ToBriefString())));
        }
    }

    private void SetValue(ModuleId module, string parameter, float value)
    {
        if (rack.SetValue(module, parameter, value).TryPickProblems(out var problems))
        {
            logger.LogError("Failed to set {Parameter}: {Problems}", parameter, string.Join("; ", problems.Select(p => p.ToBriefString())));
        }
    }
}
