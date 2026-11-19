using Olve.Engine3D.Events;
using Olve.Engine3D.Graphics.WebGPU;
using Olve.Engine3D.Systems;
using Olve.Engine3D.TimeStepping;

namespace Olve.Engine3D.Platform.Web;

public sealed class WebStepper(WebGPUScreen screen, GameClosingEvent gameClosingEvent) : ITimeStepper
{
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TimeSpan? _lastTimestamp;
    private bool _closeRequested;

    public Event Load { get; } = new();
    public Event<TimeSpan> Render { get; } = new();
    public Event<TimeSpan> Update { get; } = new();
    public Event Closing { get; } = new();

    public bool IsRunning { get; private set; }

    public Task RunAsync()
    {
        gameClosingEvent.GameClosing.Subscribe(OnGameClosing);

        Load.Invoke();
        IsRunning = true;

        if (_closeRequested)
        {
            Close();
        }

        return _closed.Task;
    }

    public void Step(TimeSpan timestamp)
    {
        var deltaTime = _lastTimestamp is { } last ? timestamp - last : TimeSpan.Zero;
        _lastTimestamp = timestamp;

        Update.Invoke(deltaTime);

        if (!_closeRequested)
        {
            Render.Invoke(deltaTime);
            screen.Present();
        }

        if (_closeRequested)
        {
            Close();
        }
    }

    public void Dispose()
    {
    }

    private void OnGameClosing() => _closeRequested = true;

    private void Close()
    {
        gameClosingEvent.GameClosing.Unsubscribe(OnGameClosing);
        IsRunning = false;
        Closing.Invoke();
        _closed.TrySetResult();
    }
}
