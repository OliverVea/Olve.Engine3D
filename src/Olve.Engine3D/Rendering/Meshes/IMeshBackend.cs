namespace Olve.Engine3D.Rendering.Meshes;

/// <summary>Creates, updates and deletes meshes on the GPU.</summary>
public interface IMeshBackend
{
    Result CreateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers)
        where TVertex : IVertexData;

    Result UpdateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers)
        where TVertex : IVertexData;

    DeletionResult DeleteMesh(MeshId meshId);
}
