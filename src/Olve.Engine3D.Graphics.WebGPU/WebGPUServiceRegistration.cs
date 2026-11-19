using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Registers the WebGPU backends. The platform creates the <see cref="WebGPUContext"/>, registers the <see cref="IScreenPresenter"/> and presents the <see cref="WebGPUScreen"/>.</summary>
public static class WebGPUServiceRegistration
{
    public static IServiceCollection AddWebGPUServices(this IServiceCollection services)
    {
        services.TryAddSingleton<Provider<WebGPUContext>>();
        services.TryAddSingleton<WebGPUScreen>();
        services.TryAddSingleton<WebGPUGpuTimer>();

        services.TryAddSingleton<WebGPUResources>();
        services.TryAddSingleton<ITextureBackend, WebGPUTextureBackend>();
        services.TryAddSingleton<ISamplerBackend, WebGPUSamplerBackend>();
        services.TryAddSingleton<IMeshBackend, WebGPUMeshBackend>();
        services.TryAddSingleton<IGroupBackend, WebGPUGroupBackend>();
        services.TryAddSingleton<IShaderBackend, WebGPUShaderBackend>();
        services.TryAddSingleton<IPassBackend, WebGPUPassBackend>();
        services.TryAddSingleton<IDrawBackend, WebGPUDrawBackend>();
        services.TryAddSingleton<IFrameBackend, WebGPUFrameBackend>();
        return services;
    }
}
