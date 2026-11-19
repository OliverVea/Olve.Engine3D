using Microsoft.Extensions.DependencyInjection;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Input.Actions;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Samples.Synth.Space;
using Olve.Engine3D.Scenes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Samples.Synth;

/// <summary>
/// A small modular synthesizer: a row of modules patched with cables, played from the computer keyboard. The patch lives
/// here; the host adds the <see cref="ISynthAudio"/> that makes it heard and a scene service that draws the rack.
/// </summary>
public static class SynthSample
{
    public static readonly Id<IScene> SceneId = Id.New<IScene>();

    public static IServiceCollection AddSynthSample(this IServiceCollection services)
    {
        services.AddEngineServices();
        services.AddInputActions([], []);

        services.AddSingleton(new StartScene(SceneId));
        services.AddSingleton<IEnumerable<SceneDefinition>>([new SceneDefinition(SceneId, "SynthScene")]);
        services.AddSingleton(new SynthOptions());
        services.AddSingleton<SynthRack>();
        services.AddSceneService<AudioFrameService>(SceneId);
        services.AddSceneService<RackInputService>(SceneId);
        services.AddSceneService<RackLightsService>(SceneId);
        services.AddSceneService<ScopeService>(SceneId);
        services.AddSceneService<OutputLevelsService>(SceneId);
        services.AddSceneService<SpeakerLevelsService>(SceneId);
        services.AddSceneService<CommandProcessingService>(SceneId);

        return services;
    }
}
