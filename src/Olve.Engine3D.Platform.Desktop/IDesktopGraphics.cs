using Silk.NET.Windowing;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>The graphics API the window draws with: sets up its context or surface and presents each rendered frame.</summary>
public interface IDesktopGraphics
{
    Task<Result> InitializeAsync(IWindow window);

    void Present();
}
