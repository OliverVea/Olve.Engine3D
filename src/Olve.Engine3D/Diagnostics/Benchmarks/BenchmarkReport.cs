using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olve.Engine3D.Diagnostics.Benchmarks;

/// <summary>A benchmark run's settings, the machine it ran on, its metrics and its raw frame times.</summary>
public sealed record BenchmarkReport(
    string Scenario,
    string Backend,
    string Adapter,
    string Resolution,
    int Samples,
    bool VSync,
    BenchmarkMachine Machine,
    int WarmupFrames,
    int Frames,
    double? StartupToFirstFrameMs,
    BenchmarkMetrics Metrics,
    BenchmarkFrames Raw)
{
    public static BenchmarkReport Create(
        BenchmarkSettings settings,
        BenchmarkGraphics? graphics,
        Vector2<int> resolution,
        double? startupToFirstFrameMs,
        double[] frameMs,
        double[] cpuMs,
        double[] waitMs,
        double[] gpuMs,
        int[] collections,
        int gen2Collections)
    {
        double?[] gpu = [.. gpuMs.Select(ms => double.IsNaN(ms) ? (double?)null : ms)];
        return new BenchmarkReport(
            settings.Scenario,
            graphics?.Backend ?? "unknown",
            graphics?.Adapter ?? "unknown",
            $"{resolution.X}x{resolution.Y}",
            graphics?.Samples ?? 0,
            graphics?.VSync ?? false,
            BenchmarkMachine.Describe(),
            settings.WarmupFrames,
            settings.Frames,
            startupToFirstFrameMs,
            BenchmarkMetrics.Compute(frameMs, cpuMs, gpuMs) with
            {
                WaitMs = BenchmarkDistribution.Of([.. waitMs.Order()]),
                GcCollections = collections.Sum(),
                Gen2Collections = gen2Collections,
            },
            new BenchmarkFrames(frameMs, cpuMs, waitMs, gpu, collections));
    }

    public static Result Write(BenchmarkReport report, string path)
    {
        try
        {
            if (System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) is { } directory)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(report, BenchmarkJsonContext.Default.BenchmarkReport));
            return Result.Success();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new ResultProblem(e, "Error while writing the benchmark report to {0}", path);
        }
    }
}

/// <summary>
/// Frame rates, time distributions and the garbage collections during the run. A low is the frame rate over the slowest share of frames: the 1% low is the
/// number of frames in the slowest 1% divided by the time they took.
/// </summary>
public sealed record BenchmarkMetrics(
    double AverageFps,
    double Low5PercentFps,
    double Low1PercentFps,
    BenchmarkDistribution FrameMs,
    BenchmarkDistribution CpuMs,
    BenchmarkDistribution? GpuMs,
    int GpuFrames,
    BenchmarkDistribution? WaitMs = null,
    int GcCollections = 0,
    int Gen2Collections = 0)
{
    public static BenchmarkMetrics Compute(double[] frameMs, double[] cpuMs, double[] gpuMs)
    {
        double[] sorted = [.. frameMs.Order()];
        double[] gpu = [.. gpuMs.Where(ms => !double.IsNaN(ms)).Order()];
        return new BenchmarkMetrics(
            GetFps(sorted, sorted.Length),
            GetFps(sorted, GetSlowestCount(sorted.Length, 0.05)),
            GetFps(sorted, GetSlowestCount(sorted.Length, 0.01)),
            BenchmarkDistribution.Of(sorted),
            BenchmarkDistribution.Of([.. cpuMs.Order()]),
            gpu.Length > 0 ? BenchmarkDistribution.Of(gpu) : null,
            gpu.Length);
    }

    // At least one frame, so short runs still have a low
    private static int GetSlowestCount(int frames, double share) => System.Math.Max(1, (int)(frames * share));

    // Frames per second over the slowest count frames of a sorted list
    private static double GetFps(double[] sortedMs, int count)
    {
        if (sortedMs.Length == 0)
        {
            return 0;
        }

        var totalMs = sortedMs[^count..].Sum();
        return totalMs > 0 ? count * 1000 / totalMs : 0;
    }
}

/// <summary>The median, 95th and 99th percentiles and the maximum of a set of times, in milliseconds.</summary>
public sealed record BenchmarkDistribution(double Median, double P95, double P99, double Max)
{
    public static BenchmarkDistribution Of(double[] sortedMs) => sortedMs.Length == 0
        ? new BenchmarkDistribution(0, 0, 0, 0)
        : new BenchmarkDistribution(GetPercentile(sortedMs, 0.5), GetPercentile(sortedMs, 0.95), GetPercentile(sortedMs, 0.99), sortedMs[^1]);

    // Nearest rank
    private static double GetPercentile(double[] sortedMs, double percentile) =>
        sortedMs[System.Math.Clamp((int)System.Math.Ceiling(percentile * sortedMs.Length) - 1, 0, sortedMs.Length - 1)];
}

/// <summary>
/// Each measured frame's frame-to-frame time, CPU time (update through submit, without waiting), time spent waiting (for
/// the screen's next texture), GPU time, and the garbage collections that started during it; a GPU time is null when it
/// wasn't measured.
/// </summary>
public sealed record BenchmarkFrames(double[] FrameMs, double[] CpuMs, double[] WaitMs, double?[] GpuMs, int[] GcCollections);

/// <summary>The machine a benchmark ran on.</summary>
public sealed record BenchmarkMachine(string Cpu, int LogicalProcessors, string Os, string Runtime)
{
    public static BenchmarkMachine Describe() => new(
        GetCpuName(), Environment.ProcessorCount, RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription);

    private static string GetCpuName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "unknown";
            }

            if (OperatingSystem.IsLinux())
            {
                var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
                return line?.Split(':', 2)[1].Trim() ?? "unknown";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return "unknown";
        }

        return "unknown";
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BenchmarkReport))]
internal sealed partial class BenchmarkJsonContext : JsonSerializerContext;
