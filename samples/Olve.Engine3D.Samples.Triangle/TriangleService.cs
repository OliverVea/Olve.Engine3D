using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Scenes;
using Olve.Generated.Shaders;
using Olve.Results;
using System.Numerics;

namespace Olve.Engine3D.Samples.Triangle;

/// <summary>Draws the triangle into the screen and renders each frame.</summary>
public class TriangleService(
    RenderingSegment segment,
    PassManager passManager,
    ShaderManager shaderManager,
    MeshManager meshManager,
    GroupManager groupManager,
    DrawManager drawManager,
    RenderingManager renderingManager) : ISceneService
{
    private static readonly ITextureFormat.Rgba8Unorm Background = new(20, 20, 30, 255);

    public Result Load()
    {
        var screen = ColorAttachment.Of(TextureManager.Screen, new Load<ITextureFormat.Rgba8Unorm>.Clear(Background));
        if (passManager.Register(screen, new IPassInputs.None(), priority: 0, segment).TryPickProblems(out var problems, out var passId))
        {
            return problems.Prepend("Failed to create the screen pass");
        }

        if (shaderManager.Register(Shaders.Triangle.Definition, segment).TryPickProblems(out problems, out var shaderId))
        {
            return problems.Prepend("Failed to register the triangle shader");
        }

        // Three vertices without data: the shader builds the corners from the vertex index
        var mesh = new MeshBuffers<IVertexData.None> { Vertices = [default, default, default] };
        if (meshManager.Register(mesh, segment).TryPickProblems(out problems, out var meshId))
        {
            return problems.Prepend("Failed to register the triangle mesh");
        }

        if (groupManager.Register<IVertexData.None, Shaders.Triangle.Instance>(meshId, segment).TryPickProblems(out problems, out var groupId)
            || groupManager.AddInstance(groupId, new Shaders.Triangle.Instance(Vector2.Zero)).TryPickProblems(out problems, out _))
        {
            return problems.Prepend("Failed to create the triangle group");
        }

        return drawManager.Register(passId, shaderId, groupId, new IGroupInputs.None(), RenderState.Opaque, key: 0, segment)
            .TryPickProblems(out problems, out _)
            ? problems.Prepend("Failed to create the triangle draw")
            : Result.Success();
    }

    public Result Render() => renderingManager.Render();
}
