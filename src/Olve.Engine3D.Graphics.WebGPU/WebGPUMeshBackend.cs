using System.Runtime.CompilerServices;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;
using WgpuBuffer = Olve.WebGPU.Buffer;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Meshes as a vertex buffer and an optional index buffer, replaced when an update no longer fits.</summary>
internal sealed class WebGPUMeshBackend(Provider<WebGPUContext> contextProvider, WebGPUResources resources) : IMeshBackend
{
    public Result CreateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers)
        where TVertex : IVertexData
    {
        if (resources.Meshes.ContainsKey(meshId))
        {
            return new ResultProblem("Mesh '{0}' already exists", meshId);
        }

        if (Validate(meshBuffers).TryPickProblems(out var problems))
        {
            return problems;
        }

        var mesh = new WgpuMesh();
        Upload(contextProvider.Value, mesh, meshBuffers);
        resources.Meshes[meshId] = mesh;
        return Result.Success();
    }

    public Result UpdateMesh<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers)
        where TVertex : IVertexData
    {
        if (!resources.Meshes.TryGetValue(meshId, out var mesh))
        {
            return new ResultProblem("Mesh '{0}' has no WebGPU buffers", meshId);
        }

        if (Validate(meshBuffers).TryPickProblems(out var problems))
        {
            return problems;
        }

        Upload(contextProvider.Value, mesh, meshBuffers);
        return Result.Success();
    }

    public DeletionResult DeleteMesh(MeshId meshId)
    {
        if (!resources.Meshes.Remove(meshId, out var mesh))
        {
            return DeletionResult.NotFound();
        }

        mesh.Release();
        return DeletionResult.Success();
    }

    private static Result Validate<TVertex>(MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
    {
        if (meshBuffers.Indices is { } indices && indices.Any(index => index >= meshBuffers.Vertices.Length))
        {
            return new ResultProblem("The mesh's indices reach past its {0} vertices", meshBuffers.Vertices.Length);
        }

        return Result.Success();
    }

    private static void Upload<TVertex>(WebGPUContext context, WgpuMesh mesh, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
    {
        mesh.VertexCount = (uint)meshBuffers.Vertices.Length;

        if (TVertex.FloatCount > 0 && meshBuffers.Vertices.Length > 0)
        {
            var floats = new float[meshBuffers.Vertices.Length * TVertex.FloatCount];
            for (var i = 0; i < meshBuffers.Vertices.Length; i++)
            {
                meshBuffers.Vertices[i].WriteTo(floats.AsSpan(i * TVertex.FloatCount, TVertex.FloatCount));
            }

            var (vertexBuffer, vertexSize) = Write(context, mesh.VertexBuffer, mesh.VertexBufferSize, BufferUsage.Vertex, (ReadOnlySpan<float>)floats);
            mesh.VertexBuffer = vertexBuffer;
            mesh.VertexBufferSize = vertexSize;
        }

        if (meshBuffers.Indices is not { Length: > 0 } indices)
        {
            mesh.IndexBuffer.Dispose();
            mesh.IndexBuffer = default;
            mesh.IndexBufferSize = 0;

            mesh.IndexCount = 0;
            return;
        }

        var (indexBuffer, indexSize) = Write(context, mesh.IndexBuffer, mesh.IndexBufferSize, BufferUsage.Index, indices.AsSpan());
        mesh.IndexBuffer = indexBuffer;
        mesh.IndexBufferSize = indexSize;
        mesh.IndexCount = (uint)indices.Length;
    }

    // Replaces the buffer with a new one if the data no longer fits
    private static (WgpuBuffer Buffer, ulong Size) Write<T>(WebGPUContext context, WgpuBuffer buffer, ulong bufferSize, BufferUsage usage, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        var size = (ulong)(data.Length * Unsafe.SizeOf<T>());
        if (buffer == default || size > bufferSize)
        {
            buffer.Dispose();

            buffer = context.CreateBuffer(usage | BufferUsage.CopyDst, size);
            bufferSize = size;
        }

        context.Queue.WriteBuffer(buffer, 0, data);
        return (buffer, bufferSize);
    }
}
