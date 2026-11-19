using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Input.Actions;
using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Input;

public class InputActionManagerTests
{
    private static readonly ButtonAction Rotate = new("Rotate", [Key.R]);
    private static readonly ButtonAction Reload = new("Reload", [Key.R]);
    private static readonly ButtonAction Zoom = new("Zoom", [Key.Z, Key.Equal]);
    private static readonly ButtonAction Unregistered = new("Unregistered", [Key.U]);
    private static readonly DirectionalAction Move = new("Move", [new DirectionalBinding(Key.W, Key.S, Key.A, Key.D)]);

    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(200, 100) }, [Pointer.Mouse]);

    private InputActionManager CreateManager(InputBindingOverrides? overrides = null) =>
        new(_input, (overrides ?? InputBindingOverrides.None).Resolve([Rotate, Reload, Zoom, Move]),
            NullLogger<InputActionManager>.Instance);

    private void RunFrame() => _input.Apply(_events.Take());

    [Test]
    public async Task An_Action_Triggers_By_Any_Of_Its_Bindings()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.Equal);
        RunFrame();

        await Assert.That(manager.WasTriggered(Zoom)).IsTrue();
        await Assert.That(manager.IsDown(Zoom)).IsTrue();
    }

    [Test]
    public async Task Consuming_An_Action_Hides_Its_Buttons_From_Other_Actions()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.TryConsume(Rotate)).IsTrue();
        await Assert.That(manager.WasTriggered(Reload)).IsFalse();
        await Assert.That(manager.TryConsume(Rotate)).IsFalse();
        await Assert.That(_input.IsConsumed(Key.R)).IsTrue();
    }

    [Test]
    public async Task A_Consumed_Button_Does_Not_Trigger_The_Action()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.R);
        RunFrame();
        _input.Consume(Key.R);

        await Assert.That(manager.WasTriggered(Rotate)).IsFalse();
    }

    [Test]
    public async Task Overrides_Replace_The_Default_Bindings()
    {
        var overrides = new InputBindingOverrides
        {
            ButtonBindings = new Dictionary<string, IReadOnlyList<ButtonBinding>> { ["rotate"] = [Key.T] },
        };
        var manager = CreateManager(overrides);

        _events.AddButtonDown(Key.R);
        _events.AddButtonDown(Key.T);
        RunFrame();

        await Assert.That(manager.GetBindings(Rotate)).IsEquivalentTo(new ButtonBinding[] { Key.T });
        await Assert.That(manager.TryConsume(Rotate)).IsTrue();
        await Assert.That(_input.IsConsumed(Key.R)).IsFalse();
    }

    [Test]
    public async Task An_Empty_Override_Leaves_The_Action_Unbound()
    {
        var overrides = new InputBindingOverrides
        {
            ButtonBindings = new Dictionary<string, IReadOnlyList<ButtonBinding>> { ["Rotate"] = [] },
        };
        var manager = CreateManager(overrides);

        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(manager.WasTriggered(Rotate)).IsFalse();
    }

    [Test]
    public async Task An_Unregistered_Action_Has_No_Bindings()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.U);
        RunFrame();

        await Assert.That(manager.GetBindings(Unregistered)).IsEmpty();
        await Assert.That(manager.TryConsume(Unregistered)).IsFalse();
    }

    [Test]
    public async Task A_Directional_Action_Sums_Its_Held_Buttons_Without_Consuming_Them()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.W);
        _events.AddButtonDown(Key.D);
        RunFrame();

        await Assert.That(manager.GetValue(Move)).IsEqualTo(new Vector2(1, 1));
        await Assert.That(_input.IsConsumed(Key.W)).IsFalse();
    }

    [Test]
    public async Task Opposite_Directions_Cancel_Out()
    {
        var manager = CreateManager();

        _events.AddButtonDown(Key.A);
        _events.AddButtonDown(Key.D);
        RunFrame();

        await Assert.That(manager.GetValue(Move)).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Actions_Are_Listed_In_Registration_Order()
    {
        var manager = CreateManager();

        await Assert.That(manager.Actions.Select(action => action.Name))
            .IsEquivalentTo(new[] { "Rotate", "Reload", "Zoom", "Move" });
    }
}
