using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Assets;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.OpenAL;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Graphics.WebGPU;
using Olve.Engine3D.Input;
using Olve.Engine3D.Logging;
using Olve.Engine3D.Platform.Desktop.Commands;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Storage;
using Olve.Engine3D.TimeStepping;
using Olve.Paths;
using Olve.Utilities.Ids;
using Silk.NET.GLFW;
using Silk.NET.Windowing;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// Runs a game in a desktop window, or sends a command to a running instance.
/// </summary>
public static class DesktopHost
{
    /// <summary>
    /// Opens the window and runs the game until it exits. Returns the process exit code.
    /// </summary>
    /// <param name="addGame">Registers the game's and the engine's services, including the <see cref="StartScene"/>.</param>
    public static int Run(
        DesktopHostOptions options,
        Action<IServiceCollection> addGame)
    {
        if (string.IsNullOrWhiteSpace(options.AppName))
        {
            return Fail([new ResultProblem("The host needs an app name to keep the user's storage under")]);
        }

        var instanceId = options.InstanceId is not null
            ? new GameInstanceId(options.InstanceId)
            : new GameInstanceId();

        if (OperatingSystem.IsLinux())
        {
            UseX11();
        }

        var windowOptions = CreateWindowOptions(options);

        if (options.Resolution is { } resolution)
        {
            windowOptions = windowOptions with
            {
                Size = resolution.ToSilk(),
                WindowBorder = WindowBorder.Hidden,
                WindowState = WindowState.Normal,
            };
        }

        var services = new ServiceCollection();
        addGame(services);
        AddDesktopServices(services, Window.Create(windowOptions), instanceId, options);

        using var serviceProvider = GameHost.BuildServiceProvider(services);

        var logger = serviceProvider.GetRequiredService<ILogger<GameManager>>();
        logger.LogInformation("Starting {Title}", options.Window.Title);
        if (options.Listen)
        {
            logger.LogInformation("Listening on pipe '{PipeName}'", instanceId.GetPipeName());
        }

        if (RunPumpingFrameContext(serviceProvider).TryPickProblems(out var problems))
        {
            return Fail(problems);
        }

        Console.WriteLine("Game exited successfully.");
        return 0;
    }

    private static int Fail(IEnumerable<ResultProblem> problems)
    {
        foreach (var problem in problems)
        {
            Console.Error.WriteLine(problem.ToDebugString());
        }

        return 1;
    }

    /// <summary>
    /// Sends a command to a running instance (started with --listen) and prints its response.
    /// Returns the process exit code.
    /// </summary>
    public static async Task<int> SendCommandAsync(string command, string? instanceId)
    {
        var pipeName = instanceId is not null
            ? new GameInstanceId(instanceId).GetPipeName()
            : GameInstanceId.GetDefaultPipeName();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(20));
        var result = await CommandPipeClient.SendCommandAsync(pipeName, command, cts.Token);

        if (result.TryPickProblems(out var problems, out var response))
        {
            foreach (var problem in problems)
            {
                await Console.Error.WriteLineAsync(problem.ToDebugString());
            }

            return 1;
        }

        if (response.Success)
        {
            if (!string.IsNullOrEmpty(response.Output))
            {
                Console.WriteLine(response.Output);
            }

            return 0;
        }

        foreach (var error in response.Errors)
        {
            await Console.Error.WriteLineAsync(error);
        }

        return 1;
    }

    private static Result RunPumpingFrameContext(IServiceProvider serviceProvider)
    {
        var frameContext = serviceProvider.GetRequiredService<FrameSynchronizationContext>();
        var posted = new AutoResetEvent(false);
        frameContext.OnPosted = () => posted.Set();

        var run = GameHost.RunAsync(serviceProvider);
        WaitHandle[] wakeUps = [((IAsyncResult)run).AsyncWaitHandle, posted];
        while (!run.IsCompleted)
        {
            WaitHandle.WaitAny(wakeUps);
            frameContext.RunPosted();
        }

        frameContext.OnPosted = null;
        return run.GetAwaiter().GetResult();
    }

    // Prebuilt Dawn has no Wayland surfaces (Olve.WebGPU#7), so Wayland sessions run through XWayland;
    // GLFW_PLATFORM = GLFW_PLATFORM_X11, which Silk's InitHint enum doesn't name
    private static void UseX11() => Glfw.GetApi().InitHint((InitHint)0x00050003, 0x00060004);

    // No GL context: the WebGPU surface presents, and its present mode takes the place of VSync
    private static WindowOptions CreateWindowOptions(DesktopHostOptions options) => WindowOptions.Default with
    {
        Title = options.Window.Title,
        Size = options.Window.Size.ToSilk(),
        API = GraphicsAPI.None,
        VSync = false,
    };

    private static IPath UserStorageFolder(DesktopHostOptions options) =>
        options.UserStorageRoot is { } root
            ? Olve.Paths.Path.Create(root)
            : Olve.Paths.Path.Create(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) / options.AppName / "user-storage";

    private static void AddDesktopServices(
        IServiceCollection services, IWindow window, GameInstanceId instanceId, DesktopHostOptions options)
    {
        var frameLoop = options.FrameLoop;
        services.AddSingleton(window);
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<DesktopPlatform>(sp, frameLoop));
        services.AddSingleton<IPlatform>(sp => sp.GetRequiredService<DesktopPlatform>());
        services.AddSingleton<ICursorControl>(sp => sp.GetRequiredService<DesktopPlatform>());
        services.AddSingleton(Pointer.Mouse);
        services.AddSingleton(instanceId);
        services.AddSingleton<IAudioStreamBackend, OpenALStreamBackend>();
        services.AddSoftwareAudio();
        services.AddSingleton(options.Audio);
        services.AddSingleton<IAssetSource, FileAssetSource>();
        services.AddSingleton<IUserStorage>(new FileUserStorage(UserStorageFolder(options), TimeProvider.System));
        if (options.FileExchangeFolder is { } exchangeFolder)
        {
            services.AddSingleton<IFileExchange>(new FolderFileExchange(Olve.Paths.Path.Create(exchangeFolder)));
        }
        else
        {
            services.AddSingleton<IFileExchange, NativeDialogFileExchange>();
        }

        services.AddWebGPUServices();
        services.AddSingleton<IScreenPresenter, SurfacePresenter>();
        services.AddSingleton<IDesktopGraphics>(sp => ActivatorUtilities.CreateInstance<WebGPUDesktopGraphics>(sp, options.Window));


        switch (frameLoop)
        {
            case FrameLoop.RealTime:
                services.AddSingleton<ITimeStepper, WindowStepper>();
                break;
            case FrameLoop.Manual:
                services.AddSingleton<ManualStepper>();
                services.AddSingleton(SoundRenderBudget.Whole);
                services.AddSingleton<ITimeStepper>(sp => sp.GetRequiredService<ManualStepper>());
                services.AddSingleton<StepCommandHandler>();
                services.AddSingleton<ICommandHandler>(sp => sp.GetRequiredService<StepCommandHandler>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frameLoop), frameLoop, null);
        }

        if (options.Listen)
        {
            services.AddSingleton<ICommandSource, CommandPipeServer>();
            services.AddSingleton<ICommandHandler, MarkLogCommandHandler>();
        }
    }
}
