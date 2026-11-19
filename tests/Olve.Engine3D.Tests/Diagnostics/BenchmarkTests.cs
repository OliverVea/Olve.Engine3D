using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Diagnostics.Benchmarks;
using Olve.Engine3D.Events;

namespace Olve.Engine3D.Tests.Diagnostics;

public class BenchmarkTests
{
    [Test]
    public async Task Lows_are_the_frame_rate_over_the_slowest_frames()
    {
        // 98 frames of 5 ms and two of 20 ms: the slowest 1% is one 20 ms frame, the slowest 5% is both 20 ms frames
        // and three 5 ms frames
        double[] frameMs = [.. Enumerable.Repeat(5.0, 98), 20.0, 20.0];

        var metrics = BenchmarkMetrics.Compute(frameMs, frameMs, [double.NaN]);

        await Assert.That(metrics.AverageFps).IsEqualTo(100 * 1000 / 530.0).Within(1e-9);
        await Assert.That(metrics.Low1PercentFps).IsEqualTo(50.0).Within(1e-9);
        await Assert.That(metrics.Low5PercentFps).IsEqualTo(5 * 1000 / 55.0).Within(1e-9);
        await Assert.That(metrics.FrameMs.Median).IsEqualTo(5.0);
        await Assert.That(metrics.FrameMs.P99).IsEqualTo(20.0);
        await Assert.That(metrics.FrameMs.Max).IsEqualTo(20.0);
        await Assert.That(metrics.GpuMs).IsNull();
        await Assert.That(metrics.GpuFrames).IsEqualTo(0);
    }

    [Test]
    public async Task Untimed_gpu_frames_are_left_out()
    {
        double[] frameMs = [1, 1, 1, 1];

        var metrics = BenchmarkMetrics.Compute(frameMs, frameMs, [2, double.NaN, 4, double.NaN]);

        await Assert.That(metrics.GpuFrames).IsEqualTo(2);
        await Assert.That(metrics.GpuMs!.Max).IsEqualTo(4.0);
    }

    [Test]
    public async Task A_run_measures_its_frames_after_the_warmup_then_writes_its_report_and_closes_the_game()
    {
        var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"benchmark-{Guid.NewGuid():N}.json");
        var closing = new GameClosingEvent();
        var closed = false;
        closing.GameClosing.Subscribe(() => closed = true);
        var recorder = new BenchmarkRecorder(new TestWindowInfo(), closing, NullLogger<BenchmarkRecorder>.Instance);

        await recorder.Start(new BenchmarkSettings("test", WarmupFrames: 2, Frames: 3, output, ExitWhenDone: true)).AssertSuccessAsync();

        var measured = new List<int>();
        for (var frame = 0; frame < 20 && !closed; frame++)
        {
            recorder.BeginFrame();
            measured.Add(recorder.MeasuredFrame);
            recorder.RecordGpuTime(recorder.MeasuredFrame, 1.5);
            recorder.EndFrame();
        }

        try
        {
            await Assert.That(closed).IsTrue();
            await Assert.That(measured.Take(6)).IsEquivalentTo([-1, -1, 0, 1, 2, -1]);
            await Assert.That(recorder.IsRunning).IsFalse();

            var report = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(output)).RootElement;
            await Assert.That(report.GetProperty("frames").GetInt32()).IsEqualTo(3);
            await Assert.That(report.GetProperty("raw").GetProperty("frameMs").GetArrayLength()).IsEqualTo(3);
            await Assert.That(report.GetProperty("raw").GetProperty("gcCollections").GetArrayLength()).IsEqualTo(3);
            await Assert.That(report.GetProperty("metrics").GetProperty("gpuFrames").GetInt32()).IsEqualTo(3);
            await Assert.That(report.GetProperty("resolution").GetString()).IsEqualTo("1920x1080");
        }
        finally
        {
            File.Delete(output);
        }
    }
}
