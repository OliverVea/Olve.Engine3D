using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Events;

namespace Olve.Engine3D.Diagnostics.Benchmarks;

/// <summary>What a benchmark run measures and where its report goes.</summary>
/// <param name="Scenario">A label for the report.</param>
/// <param name="WarmupFrames">Frames run before measuring.</param>
/// <param name="Frames">Frames measured.</param>
/// <param name="OutputPath">Where the JSON report is written.</param>
/// <param name="ExitWhenDone">Close the game once the report is written.</param>
public sealed record BenchmarkSettings(string Scenario, int WarmupFrames, int Frames, string OutputPath, bool ExitWhenDone);

/// <summary>The graphics a benchmark ran on, as the platform describes it.</summary>
public sealed record BenchmarkGraphics(string Backend, string Adapter, int Samples, bool VSync);

/// <summary>
/// Times frames for a benchmark run: warm-up frames, then a fixed number of measured frames, recorded into arrays
/// allocated when the run starts, so measuring allocates nothing and does no I/O. Each frame also counts the garbage
/// collections that started during it. The report is written when the run ends.
/// </summary>
public sealed class BenchmarkRecorder(IWindowInfo windowInfo, GameClosingEvent gameClosingEvent, ILogger<BenchmarkRecorder> logger)
{
    // Frames run after the last measured one, so the GPU times read back a few frames late can arrive
    private const int GpuDrainFrames = 8;

    private BenchmarkSettings? _settings;
    private BenchmarkGraphics? _graphics;
    private long[] _frameStarts = [];
    private long[] _cpuEnds = [];
    private double[] _gpuMs = [];
    private double[] _waitMs = [];
    private int[] _collections = [];
    private int _gen2CollectionsAtStart;
    private int _frame;
    private int _drainFramesLeft;
    private double? _startupToFirstFrameMs;

    /// <summary>The index of the measured frame being run, or -1 outside the measured frames.</summary>
    public int MeasuredFrame => _settings is { } settings && _frame >= settings.WarmupFrames && _frame - settings.WarmupFrames < settings.Frames
        ? _frame - settings.WarmupFrames
        : -1;

    /// <summary>Whether a run is in progress; backends time the GPU only then.</summary>
    public bool IsRunning => _settings is not null;

    public Result Start(BenchmarkSettings settings)
    {
        if (_settings is not null)
        {
            return new ResultProblem("A benchmark is already running ({0})", _settings.Scenario);
        }

        if (settings.Frames <= 0 || settings.WarmupFrames < 0)
        {
            return new ResultProblem("A benchmark needs at least one measured frame and no negative warm-up, got {0} and {1}",
                settings.Frames, settings.WarmupFrames);
        }

        logger.LogInformation("Starting benchmark '{Scenario}': {Warmup} warm-up frames, then {Frames} measured frames",
            settings.Scenario, settings.WarmupFrames, settings.Frames);

        _frameStarts = new long[settings.Frames + 1];
        _cpuEnds = new long[settings.Frames];
        _gpuMs = new double[settings.Frames];
        Array.Fill(_gpuMs, double.NaN);
        _waitMs = new double[settings.Frames];
        _collections = new int[settings.Frames + 1];
        _frame = 0;
        _drainFramesLeft = GpuDrainFrames;
        _settings = settings;
        return Result.Success();
    }

    public void DescribeGraphics(BenchmarkGraphics graphics) => _graphics = graphics;

    /// <summary>Called when a frame starts, before its update.</summary>
    public void BeginFrame()
    {
        if (_settings is not { } settings)
        {
            return;
        }

        // The start after the last measured frame ends that frame's frame-to-frame time
        var measured = _frame - settings.WarmupFrames;
        if (measured >= 0 && measured <= settings.Frames)
        {
            _frameStarts[measured] = Stopwatch.GetTimestamp();
            _collections[measured] = GC.CollectionCount(0);
            if (measured == 0)
            {
                _gen2CollectionsAtStart = GC.CollectionCount(2);
            }
        }
    }

    /// <summary>Called when a frame's commands have been submitted, before it's presented.</summary>
    public void EndFrame()
    {
        if (_startupToFirstFrameMs is null)
        {
            _startupToFirstFrameMs = GetStartupToNowMs();
        }

        if (_settings is not { } settings)
        {
            return;
        }

        var measured = _frame - settings.WarmupFrames;
        if (measured >= 0 && measured < settings.Frames)
        {
            _cpuEnds[measured] = Stopwatch.GetTimestamp();
        }

        _frame++;
        if (measured >= settings.Frames && --_drainFramesLeft <= 0)
        {
            Finish(settings);
        }
    }

    /// <summary>
    /// Records time the frame being run spent waiting rather than working, such as for the screen's next texture; it's
    /// taken out of the frame's CPU time.
    /// </summary>
    public void RecordWait(TimeSpan wait)
    {
        if (MeasuredFrame is var measured and >= 0)
        {
            _waitMs[measured] += wait.TotalMilliseconds;
        }
    }

    /// <summary>Records a measured frame's GPU time, which backends read back a few frames later.</summary>
    public void RecordGpuTime(int measuredFrame, double milliseconds)
    {
        if (measuredFrame >= 0 && measuredFrame < _gpuMs.Length)
        {
            _gpuMs[measuredFrame] = milliseconds;
        }
    }

    private void Finish(BenchmarkSettings settings)
    {
        _settings = null;

        var frameMs = new double[settings.Frames];
        var cpuMs = new double[settings.Frames];
        var collections = new int[settings.Frames];
        for (var i = 0; i < settings.Frames; i++)
        {
            frameMs[i] = Stopwatch.GetElapsedTime(_frameStarts[i], _frameStarts[i + 1]).TotalMilliseconds;
            cpuMs[i] = Stopwatch.GetElapsedTime(_frameStarts[i], _cpuEnds[i]).TotalMilliseconds - _waitMs[i];
            collections[i] = _collections[i + 1] - _collections[i];
        }

        var size = windowInfo.ContentAreaSize;
        var report = BenchmarkReport.Create(
            settings, _graphics, size, _startupToFirstFrameMs, frameMs, cpuMs, _waitMs, _gpuMs, collections,
            GC.CollectionCount(2) - _gen2CollectionsAtStart);

        if (BenchmarkReport.Write(report, settings.OutputPath).TryPickProblems(out var problems))
        {
            logger.LogError("Failed to write the benchmark report: {Problems}", problems);
        }
        else
        {
            logger.LogInformation(
                "Benchmark '{Scenario}' done: {AverageFps:F0} FPS average, {Low1Fps:F0} FPS 1% low; report written to {Path}",
                settings.Scenario, report.Metrics.AverageFps, report.Metrics.Low1PercentFps, settings.OutputPath);
        }

        if (settings.ExitWhenDone)
        {
            gameClosingEvent.GameClosing.Invoke();
        }
    }

    // The browser has no process start time
    private static double? GetStartupToNowMs() => OperatingSystem.IsBrowser()
        ? null
        : (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
}
