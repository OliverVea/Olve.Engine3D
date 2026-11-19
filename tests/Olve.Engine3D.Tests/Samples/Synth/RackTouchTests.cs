using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Math;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>Several fingers on the rack at once, each a pointer of its own.</summary>
public class RackTouchTests
{
    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(1280, 800) }, [Pointer.Mouse]);
    private readonly SynthRackTests.RecordingAudio _audio = new();
    private readonly SynthRack _rack;
    private readonly RackInputService _service;

    public RackTouchTests()
    {
        _rack = new SynthRack(_audio, NullLogger<SynthRack>.Instance);
        _service = new RackInputService(_rack, _input, new TestWindowInfo { ContentAreaSize = new Vector2<int>(1280, 800) }, new DeltaTimeService(), NullLogger<RackInputService>.Instance);
        if (_service.Load().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack didn't load");
        }

        Frame();
    }

    private void Frame()
    {
        _input.Apply(_events.Take());
        if (_service.Input().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack's input failed");
        }
    }

    // Low on the key, below the black keys that lie over the white ones
    private Vector2 Key(int semitone)
    {
        var bounds = _service.Layout.PianoKeys.Single(k => k.Semitone == semitone).Bounds;
        return _service.View.ToScreen(bounds.Min + new Vector2(bounds.Size.X / 2, bounds.Size.Y - 10));
    }

    private IEnumerable<int> Sounding() => _rack.Notes.Playing.Select(p => p.Semitone);

    private Vector2 Jack(ModuleId module, string jack) =>
        _service.View.ToScreen(_service.Layout.FindJack(new JackRef(module, jack))!.Centre);

    private void Touch(Pointer finger, Vector2 at)
    {
        _events.AddPointer(finger);
        _events.AddAxis(finger.Position, at);
        _events.AddButtonDown(finger.Primary);
    }

    private void Lift(Pointer finger)
    {
        _events.AddButtonUp(finger.Primary);
        _events.RemovePointer(finger);
    }

    [Test]
    public async Task Two_fingers_play_two_notes_at_once_and_lifting_one_leaves_the_other_sounding()
    {
        Pointer first = Pointer.Touch(1), second = Pointer.Touch(2);

        Touch(first, Key(0));
        Frame();
        Touch(second, Key(7));
        Frame();
        await Assert.That(_rack.Notes.Held).IsEquivalentTo([0, 7]);
        await Assert.That(Sounding()).IsEquivalentTo([0, 7]);
        await Assert.That(_service.Drags.Count).IsEqualTo(2);

        Lift(second);
        Frame();
        await Assert.That(_rack.Notes.Held).IsEquivalentTo([0]);
        await Assert.That(Sounding()).IsEquivalentTo([0]);

        Lift(first);
        Frame();
        Frame();
        await Assert.That(_rack.Notes.Held).IsEmpty();
        await Assert.That(_service.Drags).IsEmpty();
        await Assert.That(_input.Pointers).IsEquivalentTo([Pointer.Mouse]);
    }

    // Each step: a key or a finger on the piano's first key going down or up, reported to the frame and, unless it is
    // "polled", played as it arrives as well (as the browser does); or a frame
    public static IEnumerable<string> Arrivals() =>
    [
        "key-down frame key-up frame",
        "key-down key-up frame",
        "touch frame lift frame",
        "touch lift frame",
        "touch frame frame lift frame",
        "polled-key-down frame polled-key-up frame",
        "polled-key-down polled-key-up frame",
    ];

    [Test]
    [MethodDataSource(nameof(Arrivals))]
    public async Task A_note_played_as_its_input_arrives_sounds_once_and_stops_however_the_frames_fall(string steps)
    {
        var finger = Pointer.Touch(1);
        var key = KeyMap.Notes[0];
        foreach (var step in steps.Split(' '))
        {
            switch (step)
            {
                case "frame":
                    Frame();
                    break;
                case "key-down" or "polled-key-down":
                    _events.AddButtonDown(key);
                    if (step == "key-down")
                    {
                        _service.KeyArrived(key, isDown: true);
                    }

                    break;
                case "key-up" or "polled-key-up":
                    _events.AddButtonUp(key);
                    if (step == "key-up")
                    {
                        _service.KeyArrived(key, isDown: false);
                    }

                    break;
                case "touch":
                    Touch(finger, Key(0));
                    _service.PressArrived(finger, Key(0));
                    break;
                case "lift":
                    Lift(finger);
                    _service.ReleaseArrived(finger);
                    break;
            }
        }

        await Assert.That(_audio.Calls.Count(c => c.StartsWith("note-on"))).IsEqualTo(1);
        await Assert.That(_audio.Calls.Count(c => c.StartsWith("note-off"))).IsEqualTo(1);
        await Assert.That(_rack.Notes.Held).IsEmpty();
        await Assert.That(_service.Drags).IsEmpty();
    }

    [Test]
    public async Task A_tap_on_the_piano_while_a_cable_is_carried_drops_the_cable_and_plays_nothing()
    {
        _events.AddAxis(Pointer.Mouse.Position, Jack(Preset.Lfo, Modules.Out));
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();
        await Assert.That(_service.Carried).IsNotNull();
        _audio.Calls.Clear();

        var finger = Pointer.Touch(1);
        Touch(finger, Key(0));
        _service.PressArrived(finger, Key(0));
        Frame();
        Lift(finger);
        _service.ReleaseArrived(finger);
        Frame();

        await Assert.That(_audio.Calls.Where(c => c.StartsWith("note"))).IsEmpty();
        await Assert.That(_service.Carried).IsNull();
        await Assert.That(_service.Drags).IsEmpty();
    }

    [Test]
    public async Task A_key_played_as_its_touch_arrives_clears_the_last_cables_problem()
    {
        // Two outputs can't be joined: the hint says so
        Touch(Pointer.Touch(1), Jack(Preset.Lfo, Modules.Out));
        Frame();
        _events.AddAxis(Pointer.Touch(1).Position, Jack(Preset.Envelope, Modules.Out));
        Frame();
        Lift(Pointer.Touch(1));
        Frame();
        await Assert.That(_service.Hint.Tone).IsEqualTo(HintTone.Problem);

        var finger = Pointer.Touch(2);
        Touch(finger, Key(0));
        _service.PressArrived(finger, Key(0));

        await Assert.That(_service.Problem).IsNull();
        await Assert.That(_service.Hint.Tone).IsEqualTo(HintTone.Help);
    }

    public static IEnumerable<Func<(LineInStatus Status, string Call, string Step)>> PickerTaps() =>
    [
        () => (new LineInStatus(LineInState.Idle), "use-line-in", ""),
        () => (new LineInStatus(LineInState.Denied), "use-line-in", ""),
        () => (new LineInStatus(LineInState.NoInput), "use-line-in", ""),
        () => (new LineInStatus(LineInState.Asking), "", ""),
        () => (new LineInStatus(LineInState.Live, "Interface", 0, 1), "", ""),
        () => (new LineInStatus(LineInState.Live, "Interface", 0, 2), "step-line-in", " 1"),
    ];

    [Test]
    [MethodDataSource(nameof(PickerTaps))]
    public async Task Tapping_the_line_inputs_display_starts_it_or_once_it_plays_changes_to_the_next_input(LineInStatus status, string call, string step)
    {
        var lineIn = Preset.Module(ModuleKind.LineIn);
        _audio.LineInStatus = status;
        _audio.Calls.Clear();

        var picker = _service.Layout.Panels.Single(p => p.Module.Id == lineIn).Display!.Value;
        Touch(Pointer.Touch(1), _service.View.ToScreen(picker.Centre));
        Frame();

        await Assert.That(_audio.Log).IsEqualTo(call.Length > 0 ? $"{call} {lineIn}{step}" : "");
        await Assert.That(_service.Drags).IsEmpty();
    }

    [Test]
    [Arguments(ButtonAction.PreviousInput, "step-line-in {0} -1")]
    [Arguments(ButtonAction.NextInput, "step-line-in {0} 1")]
    public async Task The_line_inputs_buttons_step_back_and_on_through_the_inputs(ButtonAction action, string call)
    {
        var lineIn = Preset.Module(ModuleKind.LineIn);
        _audio.LineInStatus = new LineInStatus(LineInState.Live, "Interface", 1, 3);
        _audio.Calls.Clear();

        var button = _service.Layout.Buttons.Single(b => b.Module == lineIn && b.Action == action);
        Touch(Pointer.Touch(1), _service.View.ToScreen(button.Bounds.Centre));
        Frame();

        await Assert.That(_audio.Log).IsEqualTo(string.Format(System.Globalization.CultureInfo.InvariantCulture, call, lineIn));
    }

    [Test]
    public async Task One_finger_patches_a_cable_while_another_holds_a_key()
    {
        ModuleId filter = Preset.Filter, envelope = Preset.Envelope;
        Pointer playing = Pointer.Touch(1), patching = Pointer.Touch(2);

        Touch(playing, Key(4));
        Frame();
        Touch(patching, Jack(envelope, Modules.Out));
        Frame();
        _events.AddAxis(patching.Position, Jack(filter, Preset.CvOf(filter, Modules.Cutoff).Jack));
        Frame();
        await Assert.That(_service.Drags.Select(d => d.Drag.GetType().Name)).IsEquivalentTo(["PianoKey", "Cable"]);

        Lift(patching);
        Frame();
        await Assert.That(_rack.Patch.CableInto(Preset.CvOf(filter, Modules.Cutoff))?.From).IsEqualTo(new JackRef(envelope, Modules.Out));
        await Assert.That(Sounding()).IsEquivalentTo([4]);
    }

    [Test]
    public async Task A_finger_patches_the_LFO_into_the_delay_time_knobs_own_jack_while_a_key_sounds()
    {
        var delayTime = new JackRef(Preset.Delay, Modules.Delay.FindParameter(Modules.Time)!.CvJack);
        Pointer playing = Pointer.Touch(1), patching = Pointer.Touch(2);

        Touch(playing, Key(0));
        Frame();
        Touch(patching, Jack(Preset.Lfo, Modules.Out));
        Frame();
        _events.AddAxis(patching.Position, Jack(delayTime.Module, delayTime.Jack));
        Frame();
        Lift(patching);
        Frame();

        var cable = new Cable(new JackRef(Preset.Lfo, Modules.Out), delayTime);
        await Assert.That(_rack.Patch.CableInto(delayTime)).IsEqualTo(cable);
        await Assert.That(_audio.Calls).Contains($"connect {cable}");
        // The knob keeps its own setting: the LFO turns it from there
        await Assert.That(_rack.Patch.GetValue(Preset.Delay, Modules.Time)).IsEqualTo(Modules.Delay.FindParameter(Modules.Time)!.Default);
        await Assert.That(Sounding()).IsEquivalentTo([0]);
    }
}
