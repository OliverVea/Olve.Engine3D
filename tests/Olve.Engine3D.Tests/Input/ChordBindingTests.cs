using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Input.Actions;
using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Input;

public class ChordBindingTests
{
    private static readonly ButtonAction Rotate = new("Rotate", [Key.R]);
    private static readonly ButtonAction RotateBack = new("RotateBack", [new ButtonBinding(Key.ShiftLeft, Key.R)]);
    private static readonly ButtonAction Grab = new("Grab", [new ButtonBinding(Key.G, MouseButton.Left)]);

    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(200, 100) }, [Pointer.Mouse]);

    private InputActionManager CreateManager() =>
        new(_input, InputBindingOverrides.None.Resolve([Rotate, RotateBack, Grab]), NullLogger<InputActionManager>.Instance);

    private void RunFrame() => _input.Apply(_events.Take());

    [Test]
    public async Task A_Chord_Triggers_When_Its_Last_Button_Is_Pressed_While_The_Others_Are_Held()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.ShiftLeft);
        RunFrame();
        await Assert.That(manager.WasTriggered(RotateBack)).IsFalse();

        _events.AddButtonDown(Key.R);
        RunFrame();
        await Assert.That(manager.WasTriggered(RotateBack)).IsTrue();
        await Assert.That(manager.IsDown(RotateBack)).IsTrue();
    }

    [Test]
    public async Task A_Chord_Pressed_Within_One_Frame_Triggers()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.ShiftLeft);
        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.WasTriggered(RotateBack)).IsTrue();
    }

    [Test]
    public async Task A_Chord_Missing_A_Button_Does_Not_Trigger()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.WasTriggered(RotateBack)).IsFalse();
        await Assert.That(manager.WasTriggered(Rotate)).IsTrue();
    }

    [Test]
    public async Task A_Chord_Can_Mix_Devices()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.G);
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();

        await Assert.That(manager.WasTriggered(Grab)).IsTrue();
    }

    [Test]
    public async Task The_Binding_Holding_The_Most_Buttons_Wins()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.ShiftLeft);
        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.WasTriggered(RotateBack)).IsTrue();
        await Assert.That(manager.WasTriggered(Rotate)).IsFalse();
        await Assert.That(manager.IsDown(Rotate)).IsFalse();
    }

    [Test]
    public async Task The_Smaller_Binding_Applies_Again_Once_The_Larger_One_Is_Released()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.ShiftLeft);
        _events.AddButtonDown(Key.R);
        RunFrame();

        _events.AddButtonUp(Key.ShiftLeft);
        RunFrame();

        await Assert.That(manager.IsDown(RotateBack)).IsFalse();
        await Assert.That(manager.IsDown(Rotate)).IsTrue();
    }

    [Test]
    public async Task Consuming_A_Chord_Consumes_All_Its_Buttons()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.ShiftLeft);
        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.TryConsume(RotateBack)).IsTrue();
        await Assert.That(_input.IsConsumed(Key.ShiftLeft)).IsTrue();
        await Assert.That(_input.IsConsumed(Key.R)).IsTrue();
    }

    [Test]
    public async Task Modifiers_Are_Equal_In_Any_Order()
    {
        await Assert.That(new ButtonBinding(Key.ShiftLeft, Key.ControlLeft, Key.R))
            .IsEqualTo(new ButtonBinding(Key.ControlLeft, Key.ShiftLeft, Key.R));
        await Assert.That(new ButtonBinding(Key.ShiftLeft, Key.R)).IsNotEqualTo(new ButtonBinding(Key.R, Key.ShiftLeft));
        await Assert.That(new ButtonBinding(Key.ShiftLeft, Key.R)).IsNotEqualTo(new ButtonBinding(Key.R));
    }

    [Test]
    public async Task A_Held_Modifier_Does_Not_Block_A_Binding_With_Another_Trigger()
    {
        var zoom = new ButtonAction("Zoom", [Key.Z]);
        var manager = new InputActionManager(_input, InputBindingOverrides.None.Resolve([RotateBack, zoom]),
            NullLogger<InputActionManager>.Instance);

        _events.AddButtonDown(Key.ShiftLeft);
        _events.AddButtonDown(Key.Z);
        RunFrame();

        await Assert.That(manager.WasTriggered(zoom)).IsTrue();
    }

    [Test]
    public async Task The_Binding_With_More_Modifiers_Wins()
    {
        var rotateFast = new ButtonAction("RotateFast", [new ButtonBinding(Key.ShiftLeft, Key.ControlLeft, Key.R)]);
        var manager = new InputActionManager(_input, InputBindingOverrides.None.Resolve([RotateBack, rotateFast]),
            NullLogger<InputActionManager>.Instance);

        _events.AddButtonDown(Key.ShiftLeft);
        _events.AddButtonDown(Key.ControlLeft);
        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.WasTriggered(rotateFast)).IsTrue();
        await Assert.That(manager.WasTriggered(RotateBack)).IsFalse();
    }



}
