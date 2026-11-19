using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Math;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>The keybed's cheek: the pitch wheel that springs back, the mod wheel that stays, and the octave buttons.</summary>
public class KeybedTests
{
    private static readonly TimeSpan Frame60 = TimeSpan.FromSeconds(1 / 60.0);

    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(1280, 800) }, [Pointer.Mouse]);
    private readonly DeltaTimeService _time = new();
    private readonly SynthRackTests.RecordingAudio _audio = new();
    private readonly SynthRack _rack;
    private readonly RackInputService _service;

    public KeybedTests()
    {
        _rack = new SynthRack(_audio, NullLogger<SynthRack>.Instance);
        _service = new RackInputService(_rack, _input, new TestWindowInfo { ContentAreaSize = new Vector2<int>(1280, 800) }, _time,
            NullLogger<RackInputService>.Instance);
        if (_service.Load().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack didn't load");
        }

        Frame();
    }

    private void Frame(TimeSpan? elapsed = null)
    {
        _time.SetFrameDelta(elapsed ?? Frame60);
        _input.Apply(_events.Take());
        if (_service.Input().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack's input failed");
        }
    }

    private float Value(string parameter) => _rack.Patch.GetValue(Preset.Keyboard, parameter);

    private WheelLayout Wheel(string parameter) => _service.Layout.Wheels.Single(w => w.Parameter.Name == parameter);

    // Grabs the wheel in its middle and rolls it away from the player (up the screen) by the design units given
    private void Roll(WheelLayout wheel, float by)
    {
        _events.AddAxis(Pointer.Mouse.Position, _service.View.ToScreen(wheel.Bounds.Centre));
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        _events.AddAxis(Pointer.Mouse.Position, _service.View.ToScreen(wheel.Bounds.Centre - new Vector2(0, by)));
        Frame();
    }

    private void LetGo()
    {
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();
    }

    [Test]
    public async Task The_pitch_wheel_bends_while_held_and_springs_back_past_the_middle_a_little_then_settles_there()
    {
        Roll(Wheel(Modules.Bend), 50);
        var held = Value(Modules.Bend);
        await Assert.That(held).IsGreaterThan(0.5f);
        await Assert.That(_audio.Calls).Contains($"set {Preset.Keyboard} Bend {held:0.###}");

        LetGo();
        var lowest = held;
        for (var frame = 0; frame < 90; frame++)
        {
            Frame();
            lowest = float.Min(lowest, Value(Modules.Bend));
        }

        // A soft bounce: past the middle, by a fraction of how far it was held
        await Assert.That(lowest).IsLessThan(0);
        await Assert.That(lowest).IsGreaterThan(-0.3f * held);
        await Assert.That(Value(Modules.Bend)).IsEqualTo(0);
    }

    [Test]
    [Arguments(1)]
    [Arguments(5)]
    public async Task A_long_frame_lands_the_spring_where_it_would_be_never_past_where_it_started(double seconds)
    {
        Roll(Wheel(Modules.Bend), -60);
        var held = Value(Modules.Bend);
        LetGo();

        Frame(TimeSpan.FromSeconds(seconds));

        await Assert.That(MathF.Abs(Value(Modules.Bend))).IsLessThan(MathF.Abs(held) * 0.1f);
    }

    [Test]
    public async Task The_mod_wheel_stays_where_it_is_left_and_grabbing_the_pitch_wheel_stops_its_spring()
    {
        Roll(Wheel(Modules.Mod), 40);
        LetGo();
        var mod = Value(Modules.Mod);
        for (var frame = 0; frame < 30; frame++)
        {
            Frame();
        }

        await Assert.That(mod).IsGreaterThan(0.2f);
        await Assert.That(Value(Modules.Mod)).IsEqualTo(mod);

        Roll(Wheel(Modules.Bend), 50);
        LetGo();
        Frame();
        Roll(Wheel(Modules.Bend), 0);
        var caught = Value(Modules.Bend);
        Frame();
        Frame();
        await Assert.That(Value(Modules.Bend)).IsEqualTo(caught);
    }

    [Test]
    public async Task A_click_on_VOICES_steps_it_round_from_six_to_one_which_plays_one_key_at_a_time()
    {
        var voices = _service.Layout.Cheek!.Knobs.Single(k => k.Parameter.Name == Modules.Voices);
        _events.AddAxis(Pointer.Mouse.Position, _service.View.ToScreen(voices.Centre));
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();

        await Assert.That(Value(Modules.Voices)).IsEqualTo(1);
        await Assert.That(_rack.Notes.Voices).IsEqualTo(1);
        _rack.Press(0);
        _rack.Press(4);
        await Assert.That(_rack.Notes.Playing).IsEquivalentTo([(0, 4)]);
    }

    [Test]
    public async Task The_octave_buttons_and_Z_and_X_step_the_octave_within_three_either_way()
    {
        var up = _service.Layout.Cheek!.Buttons.Single(b => b.Action == ButtonAction.OctaveUp);
        var down = _service.Layout.Cheek.Buttons.Single(b => b.Action == ButtonAction.OctaveDown);
        void Press(ButtonLayout button)
        {
            _events.AddAxis(Pointer.Mouse.Position, _service.View.ToScreen(button.Bounds.Centre));
            _events.AddButtonDown(Pointer.Mouse.Primary);
            Frame();
            _events.AddButtonUp(Pointer.Mouse.Primary);
            Frame();
        }

        Press(up);
        Press(up);
        await Assert.That(Value(Modules.Octave)).IsEqualTo(2);
        Press(up);
        Press(up);
        await Assert.That(Value(Modules.Octave)).IsEqualTo(3);

        _events.AddButtonDown(KeyMap.OctaveDown);
        Frame();
        _events.AddButtonUp(KeyMap.OctaveDown);
        Press(down);
        await Assert.That(Value(Modules.Octave)).IsEqualTo(1);
    }
}
