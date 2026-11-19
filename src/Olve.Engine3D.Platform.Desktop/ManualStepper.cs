using Microsoft.Extensions.Logging;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Events;
using Olve.Engine3D.Systems;
using Olve.Engine3D.TimeStepping;
using Silk.NET.Windowing;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// The manual frame loop: frames advance only when the step command runs (<see cref="RunFrames"/>). Between steps it
/// handles window events and runs queued commands. A command that arrives during a step is run by the scenes'
/// <c>CommandProcessingService</c> within that step's frames.
/// </summary>
public class ManualStepper(
    IWindow window,
    IDesktopGraphics graphics,
    GameClosingEvent gameClosingEvent,
    CommandQueue commandQueue,
    CommandRunner commandRunner) : ITimeStepper
{
    // TODO: Support other frame rates
    private const float FrameTimeSeconds = 1 / 60f;
    private static readonly TimeSpan FrameTime = TimeSpan.FromSeconds(FrameTimeSeconds);
    private static readonly TimeSpan SamplePeriod = TimeSpan.FromMilliseconds(20);

    public Event Load { get; } = new();
    public Event<TimeSpan> Render { get; } = new();
    public Event<TimeSpan> Update { get; } = new();
    public Event Closing { get; } = new();

    private bool _shouldRun;

    public void Dispose()
    {
    }

    public Task RunAsync()
    {
        gameClosingEvent.GameClosing.Subscribe(OnGameClosing);

        Load.Invoke();

        _shouldRun = true;
        while (_shouldRun)
        {
            window.DoEvents();
            ProcessCommands();
            Thread.Sleep(SamplePeriod);
        }

        gameClosingEvent.GameClosing.Unsubscribe(OnGameClosing);

        Closing.Invoke();
        window.Dispose();

        return Task.CompletedTask;
    }

    private void OnGameClosing()
    {
        _shouldRun = false;
        window.Close();
    }

    /// <summary>
    /// Runs the given number of frames, then renders the last one. The step command calls this, so a step's reply
    /// is sent after its frames have run.
    /// </summary>
    public void RunFrames(ulong frames)
    {
        for (ulong frame = 0; frame < frames; frame++)
        {
            Update.Invoke(FrameTime);
        }

        Render.Invoke(FrameTime);
        graphics.Present();
    }

    private void ProcessCommands()
    {
        while (commandQueue.TryDequeue(out var pending))
        {
            // An asynchronous command replies once the steps that follow have run the frames it waits for
            _ = commandRunner.RunAndReplyAsync(pending);
        }
    }
}
