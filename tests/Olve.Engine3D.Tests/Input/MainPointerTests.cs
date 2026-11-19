using Olve.Engine3D.Input;
using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Input;

public class MainPointerTests
{
    private static readonly Pointer Keys =
        new(Olve.Utilities.Ids.Id.FromName<Pointer>("Keys"), InputAxis.Virtual(0), Key.Space, Key.Backspace);

    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(200, 100) }, [Pointer.Mouse, Keys]);

    // A connected mouse reports where it is straight away
    public MainPointerTests() => _events.AddAxis(MouseAxis.Position, new Vector2(100, 50));

    private void RunFrame() => _input.Apply(_events.Take());

    [Test]
    public async Task The_First_Pointer_Starts_As_Main()
    {
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
    }

    [Test]
    public async Task A_Device_Pointer_Starts_As_Main_Whatever_The_Order()
    {
        var input = new InputState(new TestWindowInfo { ContentAreaSize = new Vector2<int>(200, 100) }, [Keys, Pointer.Mouse]);

        await Assert.That(input.MainPointer).IsEqualTo(Pointer.Mouse);
    }

    [Test]
    public async Task Writing_A_Virtual_Pointer_Makes_It_Main()
    {
        _events.AddAxis(InputAxis.Virtual(0), new Vector2(50, 50));
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Keys);
    }

    [Test]
    public async Task Pressing_A_Pointers_Button_Makes_It_Main()
    {
        _events.AddAxis(InputAxis.Virtual(0), new Vector2(50, 50));
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();
        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);

        _events.AddButtonDown(Key.Space);
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Keys);
    }

    [Test]
    public async Task A_Virtual_Pointer_Written_Onto_The_Main_Pointer_Follows_Without_Taking_Over()
    {
        RunFrame();
        _events.AddAxis(InputAxis.Virtual(0), new Vector2(101, 50));
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
    }

    [Test]
    public async Task A_Follow_Write_A_Frame_Behind_A_Moving_Mouse_Does_Not_Take_Over()
    {
        RunFrame();

        // The keyboard pointer follows where the mouse was, while the mouse moves on in the same frame
        _events.AddAxis(MouseAxis.Position, new Vector2(150, 80));
        _events.AddAxis(InputAxis.Virtual(0), new Vector2(100, 50));
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
    }

    [Test]
    public async Task A_Pointer_That_Was_Never_Placed_Cannot_Take_Over()
    {
        _events.AddButtonDown(Key.Space);
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
    }

    [Test]
    public async Task The_Mouse_Takes_Over_Once_It_Moves_Past_A_Few_Pixels()
    {
        _events.AddAxis(MouseAxis.Position, new Vector2(100, 50));
        _events.AddAxis(InputAxis.Virtual(0), new Vector2(50, 50));
        RunFrame();
        await Assert.That(_input.MainPointer).IsEqualTo(Keys);

        _events.AddAxis(MouseAxis.Position, new Vector2(102, 51));
        RunFrame();
        await Assert.That(_input.MainPointer).IsEqualTo(Keys);

        _events.AddAxis(MouseAxis.Position, new Vector2(110, 50));
        RunFrame();
        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
    }

    [Test]
    public async Task The_Last_Use_In_A_Frame_Wins()
    {
        _events.AddAxis(InputAxis.Virtual(0), new Vector2(50, 50));
        _events.AddButtonDown(Key.Space);
        _events.AddButtonDown(MouseButton.Left);
        RunFrame();

        await Assert.That(_input.MainPointer).IsEqualTo(Pointer.Mouse);
    }
}
