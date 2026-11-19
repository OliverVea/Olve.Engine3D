using Olve.Engine3D.Events;
using Olve.Engine3D.Systems;
using Olve.Engine3D.TimeStepping;
using Silk.NET.Windowing;

namespace Olve.Engine3D.Platform.Desktop;

public class WindowStepper(IWindow window, IDesktopGraphics graphics, GameClosingEvent gameClosingEvent) : ITimeStepper
{
    public void Dispose()
    {
    }

    public Event Load { get; } = new();
    public Event<TimeSpan> Render { get; } = new();
    public Event<TimeSpan> Update { get; } = new();
    public Event Closing { get; } = new();

    public Task RunAsync()
    {
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Closing += OnClosing;
        gameClosingEvent.GameClosing.Subscribe(OnGameClosing);

        Load.Invoke();
        window.Run();

        gameClosingEvent.GameClosing.Unsubscribe(OnGameClosing);
        window.Closing -= OnClosing;
        window.Render -= OnRender;
        window.Update -= OnUpdate;

        window.Dispose();

        return Task.CompletedTask;
    }

    private void OnUpdate(double deltaSeconds) => Update.Invoke(TimeSpan.FromSeconds(deltaSeconds));

    private void OnRender(double deltaSeconds)
    {
        Render.Invoke(TimeSpan.FromSeconds(deltaSeconds));
        graphics.Present();
    }

    private void OnClosing() => Closing.Invoke();

    private void OnGameClosing() => window.Close();
}

