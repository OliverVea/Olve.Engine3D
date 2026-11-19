using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Math;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Samples.Synth.Space;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>The rack played from the seat: the mouse and fingers reach it through the seat's camera.</summary>
public class SeatedRackTests
{
    private static readonly TestWindowInfo Window = new() { ContentAreaSize = new Vector2<int>(1600, 900) };

    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(Window, [Pointer.Mouse]);
    private readonly DeltaTimeService _time = new();
    private readonly SynthRack _rack;
    private readonly SeatService _seat;
    private readonly RackInputService _service;

    public SeatedRackTests()
    {
        _rack = new SynthRack(new SynthRackTests.RecordingAudio(), NullLogger<SynthRack>.Instance);
        _seat = new SeatService(_input, Window, _time);
        _service = new RackInputService(_rack, _input, Window, _time, NullLogger<RackInputService>.Instance, _seat);
        if (_service.Load().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack didn't load");
        }

        Frame();
    }

    private void Frame()
    {
        _time.SetFrameDelta(TimeSpan.FromSeconds(1));
        _input.Apply(_events.Take());
        if (_seat.Input().TryPickProblems(out _) || _service.Input().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack's input failed");
        }
    }

    private void MoveMouse(Vector2 to)
    {
        _events.AddAxis(Pointer.Mouse.Position, to);
        Frame();
    }

    // The head turns toward the mouse, which moves what is under it, so the mouse follows the point until both settle
    private void PointAt(Vector2 design)
    {
        for (var i = 0; i < 8; i++)
        {
            MoveMouse(_service.View.ToScreen(design));
        }
    }

    [Test]
    public async Task The_head_stays_still_until_the_mouse_moves_then_follows_it()
    {
        Frame();
        await Assert.That(_seat.Seat.Look).IsEqualTo(Vector2.Zero);

        MoveMouse(new Vector2(1600, 450));
        await Assert.That(_seat.Seat.Look.X).IsGreaterThan(0.9f * SeatCamera.MaxYaw);
    }

    [Test]
    public async Task A_touch_does_not_turn_the_head()
    {
        var finger = Pointer.Touch(1);
        _events.AddPointer(finger);
        _events.AddAxis(finger.Position, new Vector2(1500, 100));
        _events.AddButtonDown(finger.Primary);
        Frame();
        _events.AddButtonUp(finger.Primary);
        _events.RemovePointer(finger);
        Frame();
        Frame();

        await Assert.That(_seat.Seat.Look).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Dragging_a_knob_up_from_the_seat_turns_it_up()
    {
        var knob = _service.Layout.Knobs.First(k => k.Parameter.Scale != ParameterScale.Stepped && Knobs.ToTurn(k.Parameter, _rack.Patch.GetValue(k.Module, k.Parameter.Name)) < 0.9f);
        var before = _rack.Patch.GetValue(knob.Module, knob.Parameter.Name);

        PointAt(knob.Centre);
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        MoveMouse(_service.View.ToScreen(knob.Centre) - new Vector2(0, 60));
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();

        await Assert.That(_rack.Patch.GetValue(knob.Module, knob.Parameter.Name)).IsGreaterThan(before);
    }

    [Test]
    public async Task Dragging_from_an_output_jack_to_a_free_input_from_the_seat_patches_a_cable()
    {
        var output = _service.Layout.Jacks.First(j => j.Definition.Direction == JackDirection.Output);
        var input = _service.Layout.Jacks.First(j => j.Definition is { Direction: JackDirection.Input, IsKnobInput: true }
                                                     && j.Jack.Module != output.Jack.Module && _rack.Patch.CableInto(j.Jack) is null);
        var cables = _rack.Patch.Cables.Count;

        PointAt(output.Centre);
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        PointAt(input.Centre);
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();

        await Assert.That(_rack.Patch.Cables.Count).IsEqualTo(cables + 1);
        await Assert.That(_rack.Patch.CableInto(input.Jack)?.From).IsEqualTo(output.Jack);
    }

    private void Click(Pointer pointer, Vector2 screen)
    {
        _events.AddAxis(pointer.Position, screen);
        _events.AddButtonDown(pointer.Primary);
        Frame();
        _events.AddButtonUp(pointer.Primary);
        Frame();
    }

    private Vector2 Screen => new(1600, 900);

    [Test]
    public async Task Tab_turns_the_seat_to_the_side_case_and_back()
    {
        _events.AddButtonDown(Key.Tab);
        Frame();
        _events.AddButtonUp(Key.Tab);
        Frame();
        await Assert.That(_seat.Seat.Turned).IsTrue();
        await Assert.That(_seat.Seat.Turn).IsEqualTo(1);

        _events.AddButtonDown(Key.Tab);
        Frame();
        _events.AddButtonUp(Key.Tab);
        Frame();
        await Assert.That(_seat.Seat.Turned).IsFalse();
        await Assert.That(_seat.Seat.Turn).IsEqualTo(0);
    }

    [Test]
    public async Task A_tap_on_the_arrow_turns_the_seat_with_a_cable_in_hand_to_plug_into_the_side_case_where_a_gate_button_flips_its_step()
    {
        // A cable picked up stays in hand through the turn, as the tap is the arrow's, not the rack's
        var output = _service.Layout.RackPanels.SelectMany(p => p.Jacks).First(j => j.Definition.Direction == JackDirection.Output);
        PointAt(output.Centre);
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();
        await Assert.That(_service.Carried).IsNotNull();

        var finger = Pointer.Touch(1);
        _events.AddPointer(finger);
        Click(finger, TurnArrow.Bounds(Screen, turned: false).Centre);
        await Assert.That(_seat.Seat.Turned).IsTrue();
        await Assert.That(_service.Carried).IsNotNull();

        // And plugged into the side case's TEMPO there, a cable runs between the cases
        var tempo = _service.Layout.SidePanels.SelectMany(p => p.Jacks).First(j => j.Definition.Knob == Modules.Tempo);
        Click(finger, _service.View.ToScreen(tempo.Centre));
        await Assert.That(_service.Carried).IsNull();
        await Assert.That(_rack.Patch.CableInto(tempo.Jack)?.From).IsEqualTo(output.Jack);

        var gate = _service.Layout.SidePanels.SelectMany(p => p.Knobs).First(k => k.Parameter.Name == Modules.StepGate(3));
        var before = _rack.Patch.GetValue(gate.Module, gate.Parameter.Name);
        Click(finger, _service.View.ToScreen(gate.Centre));
        await Assert.That(_rack.Patch.GetValue(gate.Module, gate.Parameter.Name)).IsEqualTo(1 - before);

        // The arrow is on the right now, and points back
        Click(finger, TurnArrow.Bounds(Screen, turned: true).Centre);
        await Assert.That(_seat.Seat.Turned).IsFalse();
    }

    [Test]
    public async Task The_mouse_resting_on_the_arrow_turns_the_seat_once()
    {
        MoveMouse(TurnArrow.Bounds(Screen, turned: false).Centre);
        await Assert.That(_seat.Seat.Turned).IsTrue();

        // Still resting there, where the arrow was: the seat stays turned
        Frame();
        Frame();
        await Assert.That(_seat.Seat.Turned).IsTrue();
    }
}
