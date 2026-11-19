using Microsoft.Extensions.DependencyInjection;
using Olve.Engine3D.TimeStepping;

namespace Olve.Engine3D.Platform.Web;

internal sealed class WebSession
{
    private readonly FrameSynchronizationContext _frameContext;
    private readonly WebStepper _stepper;
    private readonly Task _run;

    public WebSession(ServiceProvider provider)
    {
        _frameContext = provider.GetRequiredService<FrameSynchronizationContext>();
        Platform = provider.GetRequiredService<WebPlatform>();
        _stepper = provider.GetRequiredService<WebStepper>();
        _run = RunAsync(provider);
    }

    public WebPlatform Platform { get; }

    public FrameState Frame(TimeSpan timestamp, Vector2<int> canvasSize)
    {
        if (_run.IsCompleted)
        {
            return FrameState.Stopped;
        }

        Platform.SetCanvasSize(canvasSize);

        if (!_stepper.IsRunning)
        {
            _frameContext.RunPosted();
            return _run.IsCompleted ? FrameState.Stopped : FrameState.Starting;
        }

        _stepper.Step(timestamp);
        return FrameState.Stepped;
    }

    private static async Task RunAsync(ServiceProvider provider)
    {
        await using var _ = provider;
        await provider.GetRequiredService<WebAudioBackend>().LoadAsync();

        if ((await GameHost.RunAsync(provider)).TryPickProblems(out var problems))
        {
            foreach (var problem in problems)
            {
                await Console.Error.WriteLineAsync(problem.ToString());
            }

            return;
        }

        Console.WriteLine("Game exited successfully.");
    }
}
