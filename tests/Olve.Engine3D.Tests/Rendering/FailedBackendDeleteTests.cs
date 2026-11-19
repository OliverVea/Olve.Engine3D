using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.IntegerMath2D;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Rendering;

/// <summary>A backend that fails to delete a draw, group or pass still lets go of what that object used.</summary>
public class FailedBackendDeleteTests
{
    [Test]
    [Arguments("draw")]
    [Arguments("group")]
    [Arguments("pass")]
    public async Task A_Failed_Backend_Delete_Is_Reported_And_Releases_What_It_Used(string failing)
    {
        var backend = new FakeRenderingBackend(failing);
        var segment = Id.New<RenderingSegment>();
        var textures = new TextureManager(backend, new TestWindowInfo(), NullLogger<TextureManager>.Instance);
        var passes = new PassManager(backend, textures, NullLogger<PassManager>.Instance);
        var shaders = new ShaderManager(backend, NullLogger<ShaderManager>.Instance);
        var meshes = new MeshManager(backend, NullLogger<MeshManager>.Instance);
        var groups = new GroupManager(backend, meshes, NullLogger<GroupManager>.Instance);
        var draws = new DrawManager(backend, passes, shaders, groups, NullLogger<DrawManager>.Instance);

        var textureId = await textures.Create<ITextureFormat.Rgba8Unorm, ITextureUsage.RenderTarget>(new Size(4, 4), segment).AssertSuccessAndGetAsync();
        var passId = await passes.Register(ColorAttachment.Keep(textureId), new IPassInputs.None(), priority: 0, segment).AssertSuccessAndGetAsync();
        var shaderId = await shaders.Register(new FakeRenderingBackend.Shader(), segment).AssertSuccessAndGetAsync();
        var meshId = await meshes.Register(new MeshBuffers<IVertexData.None> { Vertices = [default, default, default] }, segment).AssertSuccessAndGetAsync();
        var groupId = await groups.Register<IVertexData.None, FakeRenderingBackend.NoInstance>(meshId, segment).AssertSuccessAndGetAsync();
        var drawId = await draws.Register(passId, shaderId, groupId, new IGroupInputs.None(), RenderState.Opaque, key: 0, segment).AssertSuccessAndGetAsync();

        // Each in the order its users let it go; a use that leaked would fail a later one with "still used by 1 consumer(s)"
        (string Kind, object Id, DeletionResult Result)[] deletions =
        [
            ("draw", drawId, draws.Delete(drawId)),
            ("shader", shaderId, shaders.Delete(shaderId)),
            ("pass", passId, passes.Delete(passId)),
            ("group", groupId, groups.Delete(groupId)),
            ("mesh", meshId, meshes.Delete(meshId)),
            ("texture", textureId, textures.Delete(textureId)),
        ];

        foreach (var (kind, id, result) in deletions)
        {
            var expected = kind == failing ? $"Failed to delete {kind} '{id}': {FakeRenderingBackend.Refused}" : "deleted";
            await Assert.That(Describe(result)).IsEqualTo(expected);
        }
    }

    private static string Describe(DeletionResult result) =>
        result.TryPickProblems(out var problems)
            ? string.Join(": ", problems.Select(problem => problem.ToBriefString()))
            : result.WasNotFound ? "not found" : "deleted";
}
