using Olve.Engine3D.TimeStepping;
using Olve.Engine3D.Audio.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Assets;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Diagnostics;
using Olve.Engine3D.Diagnostics.Benchmarks;
using Olve.Engine3D.Events;
using Olve.Engine3D.Storage;
using Olve.Engine3D.GUI;
using Olve.Engine3D.Input;
using Olve.Engine3D.Logging;
using Olve.Engine3D.Physics3D.Collisions;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Systems;
using Olve.Engine3D.Time;

namespace Olve.Engine3D;

public static class ServiceExtensions
{
    /// <summary>
    /// Registers everything the engine needs to run a game. The game registers its <see cref="StartScene"/>, scenes,
    /// input bindings and actions, and logging; the host registers the platform and the backends.
    /// </summary>
    public static IServiceCollection AddEngineServices(this IServiceCollection services)
    {
        services.AddCoreEngineServices();
        services.AddGuiServices();
        services.AddAudioServices();
        services.AddRenderingServices();

        services.AddSingleton<GameManager>();
        services.AddSingleton<FrameSynchronizationContext>();
        services.AddSingleton<SceneManager>();
        services.AddSingleton<AssetLoader>();
        services.AddSingleton<FaultLogger>();
        services.AddSingleton<BenchmarkRecorder>();
        services.AddSingleton<PlatformEvents>();
        services.AddSingleton<WindowInfo>();
        services.AddSingleton<IWindowInfo>(sp => sp.GetRequiredService<WindowInfo>());
        services.AddSingleton<InputState>();
        services.TryAddSingleton<ICursorControl, CentredCursorControl>();
        services.TryAddSingleton<IFileExchange, UnavailableFileExchange>();
        services.AddSingleton<IPlatformEventStage>(sp => sp.GetRequiredService<WindowInfo>());
        services.AddSingleton<IPlatformEventStage>(sp => sp.GetRequiredService<InputState>());
        services.AddSingleton<DeltaTimeService>();
        services.AddSingleton<DayTimeManager>();
        services.AddSingleton<AfterRenderEvent>();
        services.AddSingleton<GameClosingEvent>();
        services.AddSingleton(sp => new CommandHandlerServiceCollection(
            sp.GetRequiredService<ILogger<CommandHandlerServiceCollection>>(),
            sp.GetServices<ICommandHandler>));
        services.AddSingleton<EventQueueFactory>();
        services.TryAddScoped<CollisionSystem>();

        services.AddSingleton<CommandRunner>();
        services.AddSingleton<CommandQueue>();
        services.AddSingleton<ICommandHandler, EchoCommandHandler>();
        services.AddSingleton<ICommandHandler, LogCommandHandler>();
        services.AddSingleton<ICommandHandler, HelpCommandHandler>();
        services.AddSingleton<ICommandHandler, ExitCommandHandler>();
        services.AddSingleton<ICommandHandler, BenchmarkCommandHandler>();
        services.AddSingleton<ICommandHandler, ScreenshotCommandHandler>();
        services.AddSingleton<ICommandHandler, QueryRenderingCommandHandler>();
        services.AddSingleton<ICommandHandler, ListActionsCommandHandler>();
        services.AddSingleton<ICommandHandler, PlayTestToneCommandHandler>();
        services.AddSingleton<ICommandHandler, StopTestToneCommandHandler>();
        services.AddSingleton<ICommandHandler, QueryAudioCommandHandler>();
        services.AddSingleton<ICommandHandler, SetAudioBusCommandHandler>();
        services.AddSingleton<ICommandHandler, SetVoiceCommandHandler>();
        services.AddSingleton<ICommandHandler, StopVoiceCommandHandler>();
        services.AddSingleton<ICommandHandler, ReleaseVoiceCommandHandler>();
        services.AddSingleton<ICommandHandler, ListAudioDevicesCommandHandler>();
        services.AddSingleton<ICommandHandler, OpenAudioOutputCommandHandler>();
        services.AddSingleton<ICommandHandler, ReconfigureAudioOutputCommandHandler>();
        services.AddSingleton<ICommandHandler, CloseAudioOutputCommandHandler>();
        services.AddSingleton<ICommandHandler, RouteAudioBusCommandHandler>();

        return services;
    }

    public static IServiceCollection AddCoreEngineServices(this IServiceCollection services)
    {
        services.TryAddTransient<EntityStoreFactory>();
        services.AddSingleton<SceneScopeAccessor>();
        return services;
    }
}
