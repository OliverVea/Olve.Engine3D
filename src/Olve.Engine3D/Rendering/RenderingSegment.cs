using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering;

/// <summary>
/// What a scene scope registered with the rendering managers, which live as long as the app. Everything registered with
/// the segment is deleted when the scope ends, dependants first.
/// </summary>
public sealed class RenderingSegment(
    DrawManager drawManager,
    GroupManager groupManager,
    MeshManager meshManager,
    ShaderManager shaderManager,
    PassManager passManager,
    SamplerManager samplerManager,
    TextureManager textureManager,
    ILogger<RenderingSegment> logger) : IDisposable
{
    public Id<RenderingSegment> Id { get; } = Olve.Utilities.Ids.Id.New<RenderingSegment>();

    public static implicit operator Id<RenderingSegment>(RenderingSegment segment) => segment.Id;

    public void Dispose()
    {
        logger.LogDebug("Releasing rendering segment {SegmentId}", Id);

        var result = Result.Concat(
            drawManager.DeleteSegment(Id),
            groupManager.DeleteSegment(Id),
            meshManager.DeleteSegment(Id),
            shaderManager.DeleteSegment(Id),
            passManager.DeleteSegment(Id),
            samplerManager.DeleteSegment(Id),
            textureManager.DeleteSegment(Id));

        if (result.TryPickProblems(out var problems))
        {
            logger.LogError("Failed to release all of rendering segment {SegmentId}: {Problems}", Id, problems);
        }
    }
}
