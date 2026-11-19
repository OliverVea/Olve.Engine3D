using System.Text.Json;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Commands;

public class QueryRenderingCommandHandler(
    TextureManager textureManager,
    SamplerManager samplerManager,
    MeshManager meshManager,
    GroupManager groupManager,
    ShaderManager shaderManager,
    PassManager passManager,
    DrawManager drawManager) : ICommandHandler
{
    public string Verb => "query-rendering";
    public string HelpString => "Queries how many textures, samplers, meshes, groups, shaders, passes and draws are registered";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [];

    public Result<CommandOutput> Handle(CommandContext context) => new CommandOutput(JsonSerializer.Serialize(
        new RenderingQuery(
            Textures: textureManager.Count,
            Samplers: samplerManager.Count,
            Meshes: meshManager.Count,
            Groups: groupManager.Count,
            Shaders: shaderManager.Count,
            Passes: passManager.Count,
            Draws: drawManager.Count),
        EngineJsonContext.Default.RenderingQuery));
}

internal sealed record RenderingQuery(int Textures, int Samplers, int Meshes, int Groups, int Shaders, int Passes, int Draws);
