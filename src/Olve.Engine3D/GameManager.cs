using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Diagnostics;
using Olve.Engine3D.Diagnostics.Benchmarks;
using Olve.Engine3D.Events;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;
using Olve.Engine3D.TimeStepping;

namespace Olve.Engine3D;

public class GameManager(
    IPlatform platform,
    ITimeStepper timeStepper,
    PlatformEvents platformEvents,
    IEnumerable<IPlatformEventStage> platformEventStages,
    AudioOutputStartup audioOutputStartup,
    SoundManager soundManager,
    AudioMixer audioMixer,
    SceneManager sceneManager,
    StartScene startScene,
    DeltaTimeService deltaTimeService,
    AfterRenderEvent afterRenderEvent,
    GameClosingEvent gameClosingEvent,
    ILogger<GameManager> logger,
    FaultLogger faultLogger,
    IEnumerable<ICommandSource> commandSources,
    FrameSynchronizationContext frameContext,
    BenchmarkRecorder benchmarkRecorder)
{
    private Result _result = Result.Success();
    private readonly IPlatformEventStage[] _platformEventStages = platformEventStages.OrderBy(stage => stage.Order).ToArray();
    private readonly Stopwatch _frameSw = new();
    private const string UpdateSource = $"{nameof(GameManager)}.{nameof(Update)}";
    private const string RenderSource = $"{nameof(GameManager)}.{nameof(Render)}";

    private bool MetricsEnabled => EngineMetrics.IsEnabled;

    public async Task<Result> RunAsync()
    {
        // Awaits on the main thread resume at a fixed point in a later frame, in Update
        frameContext.OnCallbackException = ex =>
            logger.LogError(ex, "Unhandled exception in work resumed on the main thread");
        frameContext.Install();

        if ((await platform.InitializeAsync()).TryPickProblems(out var problems))
        {
            timeStepper.Dispose();
            return problems.Prepend("Error while initializing the platform");
        }

        timeStepper.Load.Subscribe(OnLoad);
        timeStepper.Render.Subscribe(OnRender);
        timeStepper.Update.Subscribe(OnUpdate);
        timeStepper.Closing.Subscribe(OnClose);

        await timeStepper.RunAsync();

        timeStepper.Dispose();

        return _result;
    }

    private void OnLoad()
    {
        if (Load().TryPickProblems(out var problems))
        {
            _result = problems;
            Stop();
        }
    }

    private Result Load()
    {
        // Event handlers are isolated: a throwing handler doesn't stop the others or the write that raised the
        // event, so log it at error level, where the integration tests' no-errors check sees it.
        EventDispatch.OnHandlerException = ex =>
            logger.LogError(ex, "Unhandled exception in an event handler; other handlers still ran");

        audioOutputStartup.Open();

        // The platform reports the window size on initialization; scenes read it while they load.
        BeginFrame();

        foreach (var commandSource in commandSources)
        {
            commandSource.Start();
        }

        return sceneManager.LoadAndActivateScene(startScene.SceneId);
    }

    private void OnUpdate(TimeSpan deltaTime)
    {
        benchmarkRecorder.BeginFrame();
        if (MetricsEnabled) _frameSw.Restart();

        deltaTimeService.SetFrameDelta(deltaTime);

        ParseFrameResult(Update(), UpdateSource);
    }

    private Result Update()
    {
        Stopwatch? inputSw = MetricsEnabled ? Stopwatch.StartNew() : null;

        BeginFrame();

        // After the frame's input is known and before the scenes see it, so resumed work sees this frame's state
        frameContext.RunPosted();

        var sceneInputResult = sceneManager.Input();
        if (sceneInputResult.TryPickProblems(out var problems))
        {
            return problems.Prepend("Got problem while processing input for SceneManager");
        }

        if (inputSw is not null)
        {
            inputSw.Stop();
            EngineMetrics.InputDuration.Record(inputSw.Elapsed.TotalMilliseconds);
        }

        Stopwatch? updateSw = MetricsEnabled ? Stopwatch.StartNew() : null;

        var sceneUpdateResult = sceneManager.Update();
        if (sceneUpdateResult.TryPickProblems(out problems))
        {
            return problems.Prepend("Got problem while updating SceneManager");
        }

        if (updateSw is not null)
        {
            updateSw.Stop();
            EngineMetrics.UpdateDuration.Record(updateSw.Elapsed.TotalMilliseconds);
        }

        return Result.Success();
    }

    /// <summary>
    /// Turns the events reported since the last frame into this frame's state, through every platform event stage.
    /// </summary>
    private void BeginFrame()
    {
        var events = platformEvents.Take();
        foreach (var stage in _platformEventStages)
        {
            events = stage.Apply(events);
        }
    }

    private void OnRender(TimeSpan deltaTime)
    {
        ParseFrameResult(Render(), RenderSource);

        soundManager.Update();
        audioMixer.Update();

        // Before the host presents, so a frame's CPU time leaves out waiting for the screen
        benchmarkRecorder.EndFrame();

        if (MetricsEnabled)
        {
            _frameSw.Stop();
            EngineMetrics.FrameDuration.Record(_frameSw.Elapsed.TotalMilliseconds);
        }
    }

    private Result Render()
    {
        Stopwatch? renderSw = MetricsEnabled ? Stopwatch.StartNew() : null;

        var result = sceneManager.Render();
        afterRenderEvent.AfterRender.Invoke();

        if (renderSw is not null)
        {
            renderSw.Stop();
            EngineMetrics.RenderDuration.Record(renderSw.Elapsed.TotalMilliseconds);
        }

        return result;
    }

    private void ParseFrameResult(Result result, string source)
    {
        if (!result.TryPickProblems(out var problems))
        {
            faultLogger.LogSuccess(source);
            return;
        }

        if (problems.AnyCritical())
        {
            StopOnCriticalProblem(problems);
            return;
        }

        faultLogger.LogFault(source, problems);
    }

    private void StopOnCriticalProblem(ResultProblemCollection problems)
    {
        logger.LogCritical("Critical problem in the frame loop; stopping the game:{NewLine}{Problems}",
            Environment.NewLine, string.Join(Environment.NewLine, problems.Select(p => p.ToDebugString())));
        _result = problems;
        Stop();
    }

    private void OnClose()
    {
        foreach (var commandSource in commandSources)
        {
            commandSource.Dispose();
        }
        sceneManager.Close();
    }

    public void Stop()
    {
        gameClosingEvent.GameClosing.Invoke();
    }
}
