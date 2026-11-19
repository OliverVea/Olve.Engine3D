using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Rendering;

public class RenderingSegmentTests
{
    private sealed class FakeMeshBackend : IMeshBackend
    {
        public Result CreateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
            => Result.Success();

        public Result UpdateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
            => Result.Success();

        public DeletionResult DeleteMesh(MeshId meshId) => DeletionResult.Success();
    }

    private static readonly MeshBuffers<IVertexData.None> Buffers = new() { Vertices = [.. new IVertexData.None[3]] };

    private static MeshId<IVertexData.None> Register(MeshManager meshes, Id<RenderingSegment> segment)
        => meshes.Register(Buffers, segment).TryPickProblems(out _, out var meshId) ? null! : meshId;

    [Test]
    public async Task Deleting_A_Segment_Deletes_Only_Its_Meshes()
    {
        var meshes = new MeshManager(new FakeMeshBackend(), NullLogger<MeshManager>.Instance);
        var menu = Id.New<RenderingSegment>();
        var game = Id.New<RenderingSegment>();
        Register(meshes, menu);
        Register(meshes, game);
        Register(meshes, game);

        await Assert.That(meshes.DeleteSegment(game)).Succeeded();

        await Assert.That(meshes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task A_Mesh_Deleted_Earlier_Is_Not_Deleted_Again()
    {
        var meshes = new MeshManager(new FakeMeshBackend(), NullLogger<MeshManager>.Instance);
        var segment = Id.New<RenderingSegment>();
        var meshId = Register(meshes, segment);

        await Assert.That(meshes.Delete(meshId).TryPickProblems(out _)).IsFalse();
        await Assert.That(meshes.DeleteSegment(segment)).Succeeded();
        await Assert.That(meshes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task An_Id_Owned_By_Two_Segments_Goes_With_The_Last()
    {
        var ownership = new SegmentOwnership<string>();
        var menu = Id.New<RenderingSegment>();
        var game = Id.New<RenderingSegment>();
        List<string> deleted = [];
        ownership.Add(menu, "shader");
        ownership.Add(game, "shader");

        ownership.Release(menu, id => { deleted.Add(id); return DeletionResult.Success(); });
        await Assert.That(deleted).IsEmpty();

        ownership.Release(game, id => { deleted.Add(id); return DeletionResult.Success(); });
        await Assert.That(deleted).IsEquivalentTo(["shader"]);
    }

    [Test]
    public async Task A_Failed_Delete_Is_Reported_And_The_Rest_Are_Still_Deleted()
    {
        var ownership = new SegmentOwnership<string>();
        var segment = Id.New<RenderingSegment>();
        List<string> deleted = [];
        ownership.Add(segment, "stuck");
        ownership.Add(segment, "free");

        var result = ownership.Release(segment, id =>
        {
            if (id == "stuck")
            {
                return new ResultProblem("still used");
            }

            deleted.Add(id);
            return DeletionResult.Success();
        });

        await Assert.That(result).Failed();
        await Assert.That(deleted).IsEquivalentTo(["free"]);
    }
}
