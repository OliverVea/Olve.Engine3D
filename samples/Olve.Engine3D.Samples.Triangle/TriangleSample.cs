using Microsoft.Extensions.DependencyInjection;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Input.Actions;
using Olve.Engine3D.Scenes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Samples.Triangle;

/// <summary>The smallest game the engine runs: one scene drawing one triangle, which chimes when clicked: panned to where, and higher towards the top.</summary>
public static class TriangleSample
{
    public static readonly Id<IScene> SceneId = Id.New<IScene>();

    public static IServiceCollection AddTriangleSample(this IServiceCollection services)
    {
        services.AddEngineServices();
        services.AddInputActions([], []);

        services.AddSingleton(new StartScene(SceneId));
        services.AddSingleton<IEnumerable<SceneDefinition>>([new SceneDefinition(SceneId, "TriangleScene")]);
        services.AddSceneService<TriangleService>(SceneId);
        services.AddSceneService<LaunchCounterService>(SceneId);
        services.AddSceneService<ChimeService>(SceneId);
        services.AddSceneService<CommandProcessingService>(SceneId);

        return services;
    }
}
