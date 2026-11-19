using System.Collections.Immutable;

namespace Olve.Engine3D.Rendering.Meshes;

/// <summary>
/// A mesh's vertices in one shader's vertex format <typeparamref name="TVertex"/> and, for an indexed mesh, its
/// indices: what <see cref="MeshManager"/> registers and the mesh backend uploads.
/// </summary>
public sealed class MeshBuffers<TVertex> where TVertex : IVertexData
{
    public required ImmutableArray<TVertex> Vertices { get; init; }

    public ImmutableArray<uint>? Indices { get; init; }
}
