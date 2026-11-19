using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Events;
using Olve.Engine3D.Rendering.Textures;
using StbImageWriteSharp;

namespace Olve.Engine3D;

/// <summary>Writes registered render targets to PNG files, read back through <see cref="TextureReadingManager"/>.</summary>
public class ScreenshotManager : IDisposable
{
    public const string ScreenTargetName = "screen";

    private static readonly byte[] WhitePixel = [255, 255, 255, 255];

    private readonly TextureReadingManager _textureReadingManager;
    private readonly AfterRenderEvent _afterRenderEvent;
    private readonly ILogger<ScreenshotManager> _logger;
    private readonly ConcurrentQueue<PendingScreenshot> _pendingScreenshots = new();
    private readonly ConcurrentQueue<IPath> _pendingFramebufferDumps = new();
    private readonly ConcurrentDictionary<string, Target> _targets = new();

    private sealed record Target(Func<ValueTask<Result<RgbaImage>>> Read, Action<byte[], int, int>? DebugOverlay);
    private sealed record RgbaImage(byte[] Pixels, int Width, int Height);
    private readonly record struct PendingScreenshot(IPath OutputPath, string? TargetName, bool Debug);

    public ScreenshotManager(
        TextureReadingManager textureReadingManager,
        AfterRenderEvent afterRenderEvent,
        ILogger<ScreenshotManager> logger)
    {
        _textureReadingManager = textureReadingManager;
        _afterRenderEvent = afterRenderEvent;
        _logger = logger;

        RegisterTarget(ScreenTargetName, TextureManager.Screen);
        afterRenderEvent.AfterRender.Subscribe(CaptureIfRequested);
    }

    public void Dispose() => _afterRenderEvent.AfterRender.Unsubscribe(CaptureIfRequested);

    public void RegisterTarget<TFormat, TUsage>(
        string name,
        TextureId<TFormat, TUsage> textureId,
        Action<byte[], int, int>? debugOverlay = null)
        where TFormat : unmanaged, ITextureFormat
        where TUsage : ITextureUsage.IRenderable
    {
        _targets[name] = new Target(async () => (await _textureReadingManager.ReadAsync(textureId)).Bind(ToRgba), debugOverlay);
        _logger.LogDebug("Screenshot target registered: {Name} ({TextureId})", name, textureId);
    }

    public void UnregisterTarget(string name)
    {
        _targets.TryRemove(name, out _);
    }

    public IReadOnlyCollection<string> TargetNames => [.. _targets.Keys];

    public void RequestScreenshot(IPath outputPath, string? targetName = null, bool debug = false)
    {
        _pendingScreenshots.Enqueue(new PendingScreenshot(outputPath, targetName, debug));
        _logger.LogDebug("Screenshot requested: {Path} (target={Target}, debug={Debug})", outputPath.Path, targetName ?? ScreenTargetName, debug);
    }

    public void RequestScreenshot(string outputPath, string? targetName = null, bool debug = false) =>
        RequestScreenshot(Path.Create(ExpandTilde(outputPath)), targetName, debug);

    public void RequestFramebufferDump(IPath outputFolder)
    {
        _pendingFramebufferDumps.Enqueue(outputFolder);
        _logger.LogDebug("Framebuffer dump requested: {Path}", outputFolder.Path);
    }

    private static string ExpandTilde(string path) =>
        path.StartsWith("~/")
            ? (Path.GetHomeDirectory() / path[2..]).Path
            : path == "~"
                ? Path.GetHomeDirectory().Path
                : path;

    // Readback is asynchronous, so each capture finishes on its own and writes its file when the pixels arrive.
    private void CaptureIfRequested()
    {
        while (_pendingScreenshots.TryDequeue(out var pending))
        {
            _ = CaptureAsync(pending.TargetName ?? ScreenTargetName, pending.OutputPath, pending.Debug);
        }

        while (_pendingFramebufferDumps.TryDequeue(out var outputFolder))
        {
            foreach (var name in _targets.Keys)
            {
                _ = CaptureAsync(name, outputFolder / $"{name}.png", debug: false);
            }
        }
    }

    private async Task CaptureAsync(string targetName, IPath outputPath, bool debug)
    {
        try
        {
            if (!_targets.TryGetValue(targetName, out var target))
            {
                _logger.LogWarning("No screenshot target '{Target}' is registered; writing a white pixel to {Path}",
                    targetName, outputPath.Path);
                WritePng(WhitePixel, 1, 1, outputPath);
                return;
            }

            if ((await target.Read()).TryPickProblems(out var problems, out var image))
            {
                _logger.LogError("Failed to capture '{Target}': {Problems}", targetName, problems);
                return;
            }

            if (debug)
            {
                target.DebugOverlay?.Invoke(image.Pixels, image.Width, image.Height);
            }

            WritePng(image.Pixels, image.Width, image.Height, outputPath);
            _logger.LogInformation("Saved {Target} to {Path}", targetName, outputPath.Absolute.Path);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to capture '{Target}' to {Path}", targetName, outputPath.Path);
        }
    }

    private static Result<RgbaImage> ToRgba<TFormat>(TextureBuffers<TFormat> texture)
        where TFormat : unmanaged, ITextureFormat
    {
        var pixels = texture.Pixels.AsSpan();
        byte[] rgba;

        if (typeof(TFormat) == typeof(ITextureFormat.Rgba8Unorm))
        {
            rgba = MemoryMarshal.AsBytes(pixels).ToArray();
            for (var i = 3; i < rgba.Length; i += 4)
            {
                rgba[i] = 255; // composite semi-transparent GUI over black
            }
        }
        else if (typeof(TFormat) == typeof(ITextureFormat.R32Float))
        {
            rgba = ToGrayscale(MemoryMarshal.Cast<TFormat, ITextureFormat.R32Float>(pixels), ignoreAtLeast: float.MaxValue);
        }
        else if (typeof(TFormat) == typeof(ITextureFormat.Depth))
        {
            // The far plane (1) is left out of the range, so what was drawn keeps its contrast
            rgba = ToGrayscale(MemoryMarshal.Cast<TFormat, ITextureFormat.R32Float>(pixels), ignoreAtLeast: 1f);
        }
        else
        {
            return new ResultProblem("Screenshots of {0} textures aren't supported", typeof(TFormat).Name);
        }

        FlipVertically(rgba, texture.Width, texture.Height);
        return new RgbaImage(rgba, texture.Width, texture.Height);
    }

    // Stretches the values' range to black..white so small differences stay visible.
    private static byte[] ToGrayscale(ReadOnlySpan<ITextureFormat.R32Float> values, float ignoreAtLeast)
    {
        var min = float.MaxValue;
        var max = float.MinValue;
        foreach (var value in values)
        {
            min = float.Min(min, value.Value);
            if (value.Value < ignoreAtLeast)
            {
                max = float.Max(max, value.Value);
            }
        }

        var range = max - min < Epsilon ? 1f : max - min;
        var rgba = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
        {
            var gray = (byte)(float.Clamp((values[i].Value - min) / range, 0f, 1f) * 255f);
            rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = gray;
            rgba[i * 4 + 3] = 255;
        }

        return rgba;
    }

    // Textures are stored from the bottom row up; PNG rows go from the top.
    private static void FlipVertically(Span<byte> pixels, int width, int height)
    {
        var rowSize = width * 4;
        Span<byte> tempRow = stackalloc byte[rowSize];

        for (var y = 0; y < height / 2; y++)
        {
            var topRow = pixels.Slice(y * rowSize, rowSize);
            var bottomRow = pixels.Slice((height - 1 - y) * rowSize, rowSize);

            topRow.CopyTo(tempRow);
            bottomRow.CopyTo(topRow);
            tempRow.CopyTo(bottomRow);
        }
    }

    private static void WritePng(byte[] pixels, int width, int height, IPath outputPath)
    {
        var absolutePath = outputPath.Absolute;
        absolutePath.Parent.EnsurePathExists();
        using var stream = File.Create(absolutePath.Path);
        new ImageWriter().WritePng(pixels, width, height, ColorComponents.RedGreenBlueAlpha, stream);
    }
}
