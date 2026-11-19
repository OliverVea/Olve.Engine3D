using Microsoft.Extensions.Logging;
using Olve.WebGPU;
using WgpuBuffer = Olve.WebGPU.Buffer;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>The WebGPU device and the window's surface, created once by the platform.</summary>
public sealed class WebGPUContext : IDisposable
{
    // Dawn answers requests on the next pump on desktop; this only catches a request that never completes
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private const ConfigureAwaitOptions ResumeOutsideCallback =
        ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.ForceYielding;

    private readonly ILogger _logger;
    private readonly DeviceLoss _loss;

    private WebGPUContext(
        Instance instance, Surface surface, Adapter adapter, string adapterDescription, Device device, DeviceLoss loss,
        SurfaceSettings surfaceSettings, ILogger logger)
    {
        AdapterDescription = adapterDescription;
        HasTimestamps = device.HasFeature(FeatureName.TimestampQuery);
        _logger = logger;
        _loss = loss;
        Instance = instance;
        Surface = surface;
        Adapter = adapter;
        Device = device;
        Queue = device.GetQueue();
        SurfaceSettings = surfaceSettings;
    }

    internal Instance Instance { get; }
    internal Surface Surface { get; }
    internal Adapter Adapter { get; }
    internal Device Device { get; }
    internal Queue Queue { get; }
    internal SurfaceSettings SurfaceSettings { get; }

    /// <summary>The GPU and the backend Dawn drives it with, e.g. "Intel(R) Arc(TM) 140T GPU (D3D12)".</summary>
    public string AdapterDescription { get; }

    /// <summary>Whether the device can time passes on the GPU (the <c>timestamp-query</c> feature).</summary>
    internal bool HasTimestamps { get; }

    /// <summary>Creates the device and a surface from the platform's window.</summary>
    public static Task<Result<WebGPUContext>> CreateAsync(in SurfaceDescriptor surfaceDescriptor, bool vSync, ILogger logger)
    {
        logger.LogDebug("Creating the WebGPU device");

        var instance = Wgpu.CreateInstance();
        if (instance.Handle == 0)
        {
            return Task.FromResult<Result<WebGPUContext>>(new ResultProblem("Failed to create a WebGPU instance"));
        }

        var surface = instance.CreateSurface(surfaceDescriptor);
        if (surface.Handle == 0)
        {
            instance.Dispose();
            return Task.FromResult<Result<WebGPUContext>>(new ResultProblem("Failed to create a WebGPU surface for the window"));
        }

        return CreateAsync(instance, surface, vSync, logger);
    }

    private static async Task<Result<WebGPUContext>> CreateAsync(Instance instance, Surface surface, bool vSync, ILogger logger)
    {
        if ((await RequestAdapterAsync(instance, surface, logger)).TryPickProblems(out var problems, out var requested))
        {
            surface.Dispose();
            instance.Dispose();
            return problems;
        }

        var (adapter, adapterDescription) = requested;

        var loss = new DeviceLoss();
        if ((await RequestDeviceAsync(instance, adapter, loss, logger)).TryPickProblems(out problems, out var device))
        {
            adapter.Dispose();
            surface.Dispose();
            instance.Dispose();
            return problems;
        }

        if (GetSurfaceSettings(surface, adapter, vSync).TryPickProblems(out problems, out var surfaceSettings))
        {
            device.Dispose();
            adapter.Dispose();
            surface.Dispose();
            instance.Dispose();
            return problems;
        }

        logger.LogInformation("Created a WebGPU device; the surface is {Format}, presenting {PresentMode}",
            surfaceSettings.Format, surfaceSettings.PresentMode);
        if ((surfaceSettings.Usage & TextureUsage.CopySrc) == 0)
        {
            logger.LogWarning("The surface can't be copied from, so screenshots won't work");
        }

        return new WebGPUContext(instance, surface, adapter, adapterDescription, device, loss, surfaceSettings, logger);
    }

    internal WgpuBuffer CreateBuffer(BufferUsage usage, ulong size) =>
        Device.CreateBuffer(new BufferDescriptor { Usage = usage, Size = size });

    internal async Task<T> AwaitAsync<T>(Task<T> request)
    {
        await request.ConfigureAwait(ResumeOutsideCallback);
        return await request;
    }

    /// <summary>A critical problem once the device is lost: nothing drawn with it reaches the screen again.</summary>
    internal Result CheckDevice() =>
        _loss.Lost is var (reason, message)
            ? new ResultProblem("The WebGPU device was lost ({0}): {1}", reason, message) { Severity = ProblemSeverities.Critical }
            : Result.Success();

    // Validation errors until PopErrorScope go to it instead of the uncaptured-error callback
    internal void PushErrorScope() => Device.PushErrorScope(ErrorFilter.Validation);

    /// <summary>
    /// The validation errors since <see cref="PushErrorScope"/>. Dawn reports them on the next pump, so on the desktop they come back here.
    /// The browser can't wait without blocking the JS event loop that resolves them, so there they are only logged when they arrive.
    /// </summary>
    internal Result PopErrorScope(string operation)
    {
        var request = Device.PopErrorScopeAsync();
        if (OperatingSystem.IsBrowser())
        {
            _ = LogScopeErrorAsync(request, operation);
            return Result.Success();
        }

        return Instance.TryProcessEventsUntil(request, RequestTimeout, out var scope)
            ? ToResult(scope, operation)
            : new ResultProblem("The WebGPU errors from {0} didn't arrive within {1}", operation, RequestTimeout);
    }

    public void Dispose()
    {
        Queue.Dispose();
        Device.Dispose();
        Adapter.Dispose();
        Surface.Dispose();
        Instance.Dispose();
    }

    private async Task LogScopeErrorAsync(Task<PopErrorScopeResult> request, string operation)
    {
        await request.ConfigureAwait(ResumeOutsideCallback);
        if (ToResult(await request, operation).TryPickProblems(out var problems))
        {
            _logger.LogError("{Problems}", string.Join(": ", problems.Select(problem => problem.ToBriefString())));
        }
    }

    private static Result ToResult(PopErrorScopeResult scope, string operation)
    {
        if (!scope.TryGetValue(out var type))
        {
            return new ResultProblem("Failed to read the WebGPU errors from {0} ({1}): {2}", operation, scope.Status, scope.Message);
        }

        return type == ErrorType.NoError
            ? Result.Success()
            : new ResultProblem("WebGPU {0} error while {1}: {2}", type, operation, scope.Message);
    }

    private static async Task<Result<T>> RequestAsync<T>(Instance instance, Task<T> request)
    {
        var pumped = instance.ProcessEventsUntilAsync(request);
        if (await Task.WhenAny(pumped, Task.Delay(RequestTimeout)) != pumped)
        {
            return new ResultProblem("the request didn't complete within {0}", RequestTimeout);
        }

        return await pumped;
    }

    private static async Task<Result<(Adapter Adapter, string Description)>> RequestAdapterAsync(Instance instance, Surface surface, ILogger logger)
    {
        var options = new RequestAdapterOptions
        {
            CompatibleSurface = surface,
            PowerPreference = PowerPreference.HighPerformance,
        };

        if ((await RequestAsync(instance, instance.RequestAdapterAsync(options))).TryPickProblems(out var problems, out var result))
        {
            return problems.Prepend("No WebGPU adapter");
        }

        if (!result.TryGetValue(out var adapter))
        {
            return new ResultProblem("No WebGPU adapter ({0}): {1}", result.Status, result.Message);
        }

        if (adapter.GetInfo(out var info) != Status.Success)
        {
            adapter.Dispose();
            return new ResultProblem("Failed to read the WebGPU adapter's info");
        }

        logger.LogInformation("WebGPU adapter: {Description} ({Backend}, {AdapterType})", info.Description, info.BackendType, info.AdapterType);

        // Dawn falls back to its Null backend, which draws nothing, when there is no usable GPU driver
        if (info.BackendType == BackendType.Null)
        {
            adapter.Dispose();
            return new ResultProblem("Only Dawn's Null backend is available: no usable GPU driver");
        }

        var name = string.IsNullOrWhiteSpace(info.Device) ? info.Description : info.Device;
        return (adapter, $"{name} ({info.BackendType})");
    }

    private static async Task<Result<Device>> RequestDeviceAsync(Instance instance, Adapter adapter, DeviceLoss loss, ILogger logger)
    {
        // Timestamp queries time frames on the GPU for benchmarks
        FeatureName[] features = adapter.HasFeature(FeatureName.TimestampQuery) ? [FeatureName.TimestampQuery] : [];
        var descriptor = new DeviceDescriptor
        {
            RequiredFeatures = features,
            DeviceLostCallbackInfo = new DeviceLostCallbackInfo
            {
                Mode = CallbackMode.AllowProcessEvents,
                Callback = (_, reason, message) => loss.Record(reason, message, logger),
            },
            UncapturedErrorCallbackInfo = new UncapturedErrorCallbackInfo
            {
                Callback = (_, type, message) => logger.LogError("WebGPU {ErrorType} error: {Message}", type, message),
            },
        };

        if ((await RequestAsync(instance, adapter.RequestDeviceAsync(descriptor))).TryPickProblems(out var problems, out var result))
        {
            return problems.Prepend("No WebGPU device");
        }

        return result.TryGetValue(out var device)
            ? device
            : new ResultProblem("No WebGPU device ({0}): {1}", result.Status, result.Message);
    }

    private static Result<SurfaceSettings> GetSurfaceSettings(Surface surface, Adapter adapter, bool vSync)
    {
        if (surface.GetCapabilities(adapter, out var capabilities) != Status.Success || capabilities.Formats.Length == 0)
        {
            return new ResultProblem("The surface has no formats for this adapter");
        }

        var formats = capabilities.Formats;

        // The screen is Rgba8Unorm and not sRGB; Bgra8Unorm is swizzled on readback
        var format = formats.Contains(TextureFormat.RGBA8Unorm) ? TextureFormat.RGBA8Unorm
            : formats.Contains(TextureFormat.BGRA8Unorm) ? TextureFormat.BGRA8Unorm
            : formats[0];

        var presentMode = !vSync && capabilities.PresentModes.Contains(PresentMode.Immediate) ? PresentMode.Immediate : PresentMode.Fifo;

        // CopySrc lets screenshots read the screen
        var usage = TextureUsage.RenderAttachment | (capabilities.Usages & TextureUsage.CopySrc);

        return new SurfaceSettings(format, presentMode, capabilities.AlphaModes[0], usage);
    }
}

/// <summary>Why the device was lost, once it is; set by the device-lost callback.</summary>
internal sealed class DeviceLoss
{
    public (DeviceLostReason Reason, string Message)? Lost { get; private set; }

    public void Record(DeviceLostReason reason, string message, ILogger logger)
    {
        // Dawn cancels the callback when the instance goes away first; the device wasn't lost
        if (reason == DeviceLostReason.CallbackCancelled)
        {
            return;
        }

        logger.LogError("WebGPU device lost ({Reason}): {Message}", reason, message);
        Lost = (reason, message);
    }
}

/// <summary>How the surface is configured: its format, present mode, alpha mode and usages.</summary>
internal readonly record struct SurfaceSettings(
    TextureFormat Format,
    PresentMode PresentMode,
    CompositeAlphaMode AlphaMode,
    TextureUsage Usage);
