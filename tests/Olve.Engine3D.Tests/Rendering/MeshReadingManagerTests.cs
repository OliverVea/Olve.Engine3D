using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Utilities.Ids;
using Olve.Results;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Rendering;

public class MeshReadingManagerTests
{
    private sealed class FakeMeshBackend : IMeshBackend
    {
        public Result CreateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
            => Result.Success();

        public Result UpdateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
            => Result.Success();

        public DeletionResult DeleteMesh(MeshId meshId) => DeletionResult.Success();
    }

    private static readonly Id<RenderingSegment> Segment = Id.New<RenderingSegment>();

    private static MeshBuffers<IVertexData.None> Buffers(int vertexCount) => new() { Vertices = [.. new IVertexData.None[vertexCount]] };

    private static (MeshManager Meshes, MeshReadingManager Reading) Create()
    {
        var meshes = new MeshManager(new FakeMeshBackend(), NullLogger<MeshManager>.Instance);
        return (meshes, new MeshReadingManager(meshes));
    }

    [Test]
    public async Task Reads_A_Registered_Mesh()
    {
        var (meshes, reading) = Create();
        var buffers = Buffers(3);
        var meshId = meshes.Register(buffers, Segment).TryPickProblems(out _, out var id) ? null! : id;

        await Assert.That(reading.Read(meshId)).Succeeded();
        await Assert.That(reading.Read(meshId).TryPickProblems(out _, out var read) ? null : read).IsSameReferenceAs(buffers);
    }

    [Test]
    public async Task Reads_The_Updated_Buffers()
    {
        var (meshes, reading) = Create();
        var meshId = meshes.Register(Buffers(3), Segment).TryPickProblems(out _, out var id) ? null! : id;
        var updated = Buffers(6);

        await Assert.That(meshes.Update(meshId, updated)).Succeeded();
        await Assert.That(reading.Read(meshId).TryPickProblems(out _, out var read) ? null : read).IsSameReferenceAs(updated);
    }

    [Test]
    public async Task Reading_A_Deleted_Mesh_Fails()
    {
        var (meshes, reading) = Create();
        var meshId = meshes.Register(Buffers(3), Segment).TryPickProblems(out _, out var id) ? null! : id;

        meshes.Delete(meshId);

        await Assert.That(reading.Read(meshId)).Failed();
    }
}
