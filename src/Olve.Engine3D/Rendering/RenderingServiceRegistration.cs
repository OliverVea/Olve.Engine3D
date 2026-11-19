using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering;

public static class RenderingServiceRegistration
{
    /// <summary>
    /// Registers the engine's rendering managers, one set for the app, and each scene scope's <see cref="RenderingSegment"/>,
    /// which deletes what the scope registered when it ends. The graphics backend registers the backend interfaces the
    /// managers use (<see cref="ITextureBackend"/>, <see cref="IPassBackend"/>, <see cref="IFrameBackend"/>, ...).
    /// </summary>
    public static IServiceCollection AddRenderingServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TextureManager>();
        services.TryAddSingleton<TextureReadingManager>();
        services.TryAddSingleton<SamplerManager>();
        services.TryAddSingleton<MeshManager>();
        services.TryAddSingleton<MeshReadingManager>();
        services.TryAddSingleton<GroupManager>();
        services.TryAddSingleton<ShaderManager>();
        services.TryAddSingleton<PassManager>();
        services.TryAddSingleton<DrawManager>();
        services.TryAddSingleton<RenderingManager>();
        services.TryAddSingleton<ScreenshotManager>();
        services.TryAddScoped<RenderingSegment>();
        return services;
    }
}
