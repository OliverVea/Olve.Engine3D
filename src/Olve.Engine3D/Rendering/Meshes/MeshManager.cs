using Olve.Utilities.Ids;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Rendering.Meshes;

/// <summary>The one source of mesh ids. Keeps each mesh's buffers, so meshes can be read back.</summary>
public class MeshManager(IMeshBackend meshBackend, ILogger<MeshManager> logger) : IDisposable
{
    private readonly Dictionary<MeshId, object> _meshes = [];
    private readonly UseCounts<MeshId> _uses = new();
    private readonly SegmentOwnership<MeshId> _owners = new();

    public Result<MeshId<TVertex>> Register<TVertex>(MeshBuffers<TVertex> meshBuffers, Id<RenderingSegment> segment) where TVertex : IVertexData
    {
        var meshId = MeshId.New<TVertex>();
        logger.LogDebug("Creating {Vertex} mesh {MeshId}", typeof(TVertex).Name, meshId);

        if (meshBackend.CreateMesh(meshId, meshBuffers).TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to create {0} mesh", typeof(TVertex).Name);
        }

        _meshes[meshId] = meshBuffers;
        _owners.Add(segment, meshId);
        logger.LogInformation("Created {Vertex} mesh {MeshId}", typeof(TVertex).Name, meshId);
        return meshId;
    }

    public Result Update<TVertex>(MeshId<TVertex> meshId, MeshBuffers<TVertex> meshBuffers) where TVertex : IVertexData
    {
        if (!_meshes.ContainsKey(meshId))
        {
            return new ResultProblem("Mesh '{0}' is not registered", meshId);
        }

        if (meshBackend.UpdateMesh(meshId, meshBuffers).TryPickProblems(out var problems))
        {
            return problems;
        }

        _meshes[meshId] = meshBuffers;
        return Result.Success();
    }

    public bool TryGetData<TVertex>(MeshId<TVertex> meshId, [MaybeNullWhen(false)] out MeshBuffers<TVertex> meshBuffers)
        where TVertex : IVertexData
    {
        if (_meshes.TryGetValue(meshId, out var data) && data is MeshBuffers<TVertex> typed)
        {
            meshBuffers = typed;
            return true;
        }

        meshBuffers = null;
        return false;
    }

    public DeletionResult Delete(MeshId meshId)
    {
        if (!_meshes.ContainsKey(meshId))
        {
            return DeletionResult.NotFound();
        }

        if (_uses.Get(meshId) is var uses and > 0)
        {
            return new ResultProblem("Mesh '{0}' is still used by {1} consumer(s)", meshId, uses);
        }

        logger.LogDebug("Deleting mesh {MeshId}", meshId);
        _meshes.Remove(meshId);
        _owners.Forget(meshId);

        var result = meshBackend.DeleteMesh(meshId);
        if (!result.TryPickProblems(out _))
        {
            logger.LogInformation("Deleted mesh {MeshId}", meshId);
        }

        return result;
    }

    /// <summary>How many meshs are registered.</summary>
    public int Count => _meshes.Count;

    /// <summary>Deletes every mesh the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    internal Result AddUse(MeshId meshId)
    {
        if (!_meshes.ContainsKey(meshId))
        {
            return new ResultProblem("Mesh '{0}' is not registered", meshId);
        }

        _uses.Add(meshId);
        return Result.Success();
    }

    internal Result ReleaseUse(MeshId meshId) =>
        _uses.TryRelease(meshId)
            ? Result.Success()
            : new ResultProblem("Mesh '{0}' has no use to release", meshId);

    public void Dispose()
    {
        MeshId[] meshIds = [.. _meshes.Keys];
        _meshes.Clear();
        _uses.Clear();
        _owners.Clear();

        foreach (var meshId in meshIds)
        {
            var result = meshBackend.DeleteMesh(meshId);
            if (result.TryPickProblems(out var problems))
            {
                logger.LogError("Failed to delete mesh {MeshId}: {Problems}", meshId, problems);
            }
            else if (result.WasNotFound)
            {
                logger.LogWarning("Mesh {MeshId} was already gone from the mesh backend", meshId);
            }
        }
    }
}
