using Microsoft.Extensions.Logging;
using Olve.Engine3D.Diagnostics.Benchmarks;
using Olve.Engine3D.Graphics.WebGPU;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;
using Silk.NET.Core.Contexts;
using Silk.NET.Windowing;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>WebGPU: a device and the window's surface, presented after each frame is rendered.</summary>
public sealed class WebGPUDesktopGraphics(
    Provider<WebGPUContext> contextProvider,
    WebGPUScreen screen,
    DesktopWindowOptions windowOptions,
    BenchmarkRecorder benchmarkRecorder,
    ILogger<WebGPUDesktopGraphics> logger) : IDesktopGraphics
{
    public async Task<Result> InitializeAsync(IWindow window)
    {
        screen.SetSamples(windowOptions.Samples);

        if (window.Native is not { } native)
        {
            return new ResultProblem("The window has no native handles to create a WebGPU surface from");
        }

        if ((await CreateContextAsync(native)).TryPickProblems(out var problems, out var context))
        {
            return problems.Prepend("Error while creating the WebGPU device");
        }

        contextProvider.Set(context);
        benchmarkRecorder.DescribeGraphics(new BenchmarkGraphics(
            "WebGPU", context.AdapterDescription, (int)screen.SampleCount, windowOptions.VSync));
        return Result.Success();
    }

    public void Present() => screen.Present();

    // The window's native handles become the surface's source
    private Task<Result<WebGPUContext>> CreateContextAsync(INativeWindow native) => native switch
    {
        { Win32: { } win32 } => CreateContextAsync(new SurfaceDescriptor
        {
            SurfaceSourceWindowsHWND = new SurfaceSourceWindowsHWND { Hinstance = win32.HInstance, Hwnd = win32.Hwnd },
        }),
        { Wayland: { } wayland } => CreateContextAsync(new SurfaceDescriptor
        {
            SurfaceSourceWaylandSurface = new SurfaceSourceWaylandSurface { Display = wayland.Display, Surface = wayland.Surface },
        }),
        { X11: { } x11 } => CreateContextAsync(new SurfaceDescriptor
        {
            SurfaceSourceXlibWindow = new SurfaceSourceXlibWindow { Display = x11.Display, Window = (ulong)x11.Window },
        }),
        _ => Task.FromResult<Result<WebGPUContext>>(new ResultProblem("No WebGPU surface source for {0} windows", native.Kind)),
    };

    private Task<Result<WebGPUContext>> CreateContextAsync(in SurfaceDescriptor surfaceDescriptor) =>
        WebGPUContext.CreateAsync(surfaceDescriptor, windowOptions.VSync, logger);
}
