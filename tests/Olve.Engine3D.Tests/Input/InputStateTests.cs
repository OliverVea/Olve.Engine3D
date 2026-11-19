using Olve.Engine3D.Input;
using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Input;

public class InputStateTests
{
    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(200, 100) }, [Pointer.Mouse]);

    private void RunFrame() => _input.Apply(_events.Take());

    [Test]
    public async Task Press_And_Release_In_One_Frame_Both_Survive()
    {
        _events.AddButtonDown(Key.R);
        _events.AddButtonUp(Key.R);
        RunFrame();

        await Assert.That(_input.WasPressed(Key.R)).IsTrue();
        await Assert.That(_input.WasReleased(Key.R)).IsTrue();
        await Assert.That(_input.IsDown(Key.R)).IsFalse();
    }

    [Test]
    public async Task Held_Button_Stays_Down_But_Is_Pressed_Only_Once()
    {
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();
        RunFrame();

        await Assert.That(_input.IsDown(MouseButton.Left)).IsTrue();
        await Assert.That(_input.WasPressed(MouseButton.Left)).IsFalse();
    }

    [Test]
    public async Task Keys_And_Mouse_Buttons_Are_Distinct()
    {
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();

        await Assert.That(_input.IsDown(Key.A)).IsFalse();
        await Assert.That(_input.IsDown(MouseButton.Left)).IsTrue();
    }

    [Test]
    public async Task Consuming_Primary_Hides_It_But_Not_Keys()
    {
        _events.AddButtonDown(MouseButton.Left);
        _events.AddButtonDown(Key.R);
        RunFrame();

        _input.Consume(_input.MainPointer.Primary);

        await Assert.That(_input.WasPressed(_input.MainPointer.Primary)).IsFalse();
        await Assert.That(_input.IsDown(_input.MainPointer.Primary)).IsFalse();
        await Assert.That(_input.WasPressed(Key.R)).IsTrue();
    }

    [Test]
    public async Task TryConsumePress_Takes_A_Press_And_Hides_It_From_Later_Readers()
    {
        _events.AddButtonDown(Key.R);
        RunFrame();

        await Assert.That(_input.TryConsumePress(Key.R)).IsTrue();
        await Assert.That(_input.WasPressed(Key.R)).IsFalse();
        await Assert.That(_input.TryConsumePress(Key.R)).IsFalse();
    }

    [Test]
    public async Task TryConsumePress_Ignores_A_Button_That_Was_Not_Pressed()
    {
        _events.AddButtonDown(Key.R);
        RunFrame();
        RunFrame();

        await Assert.That(_input.TryConsumePress(Key.R)).IsFalse();
        await Assert.That(_input.IsConsumed(Key.R)).IsFalse();
        await Assert.That(_input.IsDown(Key.R)).IsTrue();
    }

    [Test]
    public async Task TryConsumePress_Ignores_A_Press_Already_Consumed()
    {
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();
        _input.Consume(MouseButton.Left);

        await Assert.That(_input.TryConsumePress(MouseButton.Left)).IsFalse();
    }

    [Test]
    public async Task Consumption_Lasts_One_Frame()
    {
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();
        _input.Consume(MouseButton.Left);
        RunFrame();

        await Assert.That(_input.IsDown(MouseButton.Left)).IsTrue();
    }

    [Test]
    public async Task Events_Are_Kept_In_Order()
    {
        _events.AddAxis(MouseAxis.Position, new Vector2(10, 20));
        _events.AddButtonDown(MouseButton.Left);
        _events.AddAxis(MouseAxis.Position, new Vector2(30, 40));
        _events.AddButtonUp(MouseButton.Left);
        RunFrame();

        await Assert.That(_input.Events.Count).IsEqualTo(4);
        await Assert.That(_input.Events[1]).IsEqualTo(new ButtonDownEvent(MouseButton.Left));
        await Assert.That(_input.Events[2]).IsEqualTo(new AxisEvent(MouseAxis.Position, new Vector2(30, 40)));
    }

    [Test]
    public async Task Relative_Axis_Sums_Within_A_Frame_And_Resets_Next_Frame()
    {
        _events.AddAxis(MouseAxis.Scroll, new Vector2(0, 1));
        _events.AddAxis(MouseAxis.Scroll, new Vector2(0, 2));
        RunFrame();

        await Assert.That(_input.GetDelta(MouseAxis.Scroll)).IsEqualTo(new Vector2(0, 3));

        RunFrame();

        await Assert.That(_input.GetDelta(MouseAxis.Scroll)).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Absolute_Axis_Keeps_Its_Last_Value_And_Reports_Its_Change()
    {
        _events.AddAxis(MouseAxis.Position, new Vector2(50, 25));
        RunFrame();
        _events.AddAxis(MouseAxis.Position, new Vector2(100, 40));
        _events.AddAxis(MouseAxis.Position, new Vector2(150, 75));
        RunFrame();

        await Assert.That(_input.GetValue(MouseAxis.Position)).IsEqualTo(new Vector2(150, 75));
        await Assert.That(_input.GetPreviousValue(MouseAxis.Position)).IsEqualTo(new Vector2(50, 25));
        await Assert.That(_input.GetDelta(MouseAxis.Position)).IsEqualTo(new Vector2(100, 50));

        RunFrame();

        await Assert.That(_input.GetValue(MouseAxis.Position)).IsEqualTo(new Vector2(150, 75));
        await Assert.That(_input.GetDelta(MouseAxis.Position)).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Pointer_Position_Comes_From_Its_Axis()
    {
        _events.AddAxis(MouseAxis.Position, new Vector2(150, 75));
        RunFrame();

        await Assert.That(_input.GetPosition(Pointer.Mouse)).IsEqualTo(new Vector2(150, 75));
        await Assert.That(_input.GetNormalizedPosition(Pointer.Mouse)).IsEqualTo(new Vector2(0.5f, 0.5f));
    }

    [Test]
    public async Task The_First_Registered_Pointer_Is_The_Main_Pointer()
    {
        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
        await Assert.That(() => new InputState(new TestWindowInfo(), [])).Throws<ArgumentException>();
    }

    [Test]
    public async Task An_Added_Pointer_Is_Listed_Until_The_Frame_After_It_Is_Removed()
    {
        var finger = Pointer.Touch(7);
        _events.AddPointer(finger);
        _events.AddAxis(finger.Position, new Vector2(10, 20));
        _events.AddButtonDown(finger.Primary);
        RunFrame();

        await Assert.That(_input.Pointers).IsEquivalentTo([Pointer.Mouse, finger]);
        await Assert.That(_input.MainPointer).IsEqualTo(finger);
        await Assert.That(_input.GetPosition(finger)).IsEqualTo(new Vector2(10, 20));

        // The frame it lifts in still has it, so its release is read like any other
        _events.AddButtonUp(finger.Primary);
        _events.RemovePointer(finger);
        RunFrame();

        await Assert.That(_input.Pointers).Contains(finger);
        await Assert.That(_input.WasReleased(finger.Primary)).IsTrue();

        RunFrame();

        await Assert.That(_input.Pointers).IsEquivalentTo([Pointer.Mouse]);
        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
        await Assert.That(_input.GetPosition(finger)).IsEqualTo(Vector2.Zero);
    }

    [Test]
    public async Task Normalized_Position_Round_Trips()
    {
        var size = new Vector2<int>(1920, 1080);
        var normalized = new Vector2(-0.25f, 0.5f);

        var position = ScreenPosition.FromNormalized(normalized, size);

        await Assert.That(position).IsEqualTo(new Vector2(720, 810));
        await Assert.That(ScreenPosition.ToNormalized(position, size)).IsEqualTo(normalized);
    }
}
