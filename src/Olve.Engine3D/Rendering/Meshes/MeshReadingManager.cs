namespace Olve.Engine3D.Rendering.Meshes;

/// <summary>Reads meshes' vertices and indices from their CPU copy.</summary>
public class MeshReadingManager(MeshManager meshManager)
{
    public Result<MeshBuffers<TVertex>> Read<TVertex>(MeshId<TVertex> meshId) where TVertex : IVertexData
        => meshManager.TryGetData(meshId, out var meshBuffers)
            ? meshBuffers
            : new ResultProblem("Mesh '{0}' isn't registered", meshId);
}
