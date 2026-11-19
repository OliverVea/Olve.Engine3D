using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Input;

public class WindowInfoTests
{
    private readonly PlatformEvents _events = new();
    private readonly WindowInfo _window = new();

    private void RunFrame() => _window.Apply(_events.Take());

    [Test]
    public async Task Size_Is_The_Last_Reported_In_The_Frame()
    {
        _events.AddResize(new Vector2<int>(800, 600));
        _events.AddResize(new Vector2<int>(1024, 768));
        RunFrame();

        await Assert.That(_window.ContentAreaSize).IsEqualTo(new Vector2<int>(1024, 768));
    }

    [Test]
    public async Task Size_Is_Fixed_Until_The_Next_Frame()
    {
        _events.AddResize(new Vector2<int>(800, 600));
        RunFrame();
        _events.AddResize(new Vector2<int>(1024, 768));

        await Assert.That(_window.ContentAreaSize).IsEqualTo(new Vector2<int>(800, 600));
    }

    [Test]
    public async Task Minimized_Window_Keeps_The_Last_NonZero_Size()
    {
        _events.AddResize(new Vector2<int>(800, 600));
        RunFrame();
        _events.AddResize(new Vector2<int>(0, 0));
        RunFrame();

        await Assert.That(_window.ContentAreaSize).IsEqualTo(new Vector2<int>(800, 600));
    }
}
