using Olve.Utilities.Ids;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Utilities;

namespace Olve.Engine3D.Rendering.Instancing;

/// <summary>The one source of group ids: a mesh and the instances drawn with it.</summary>
public class GroupManager(IGroupBackend groupBackend, MeshManager meshManager, ILogger<GroupManager> logger) : IDisposable
{
    private readonly Dictionary<GroupId, MeshId> _groups = [];
    private readonly UseCounts<GroupId> _uses = new();
    private readonly SegmentOwnership<GroupId> _owners = new();

    public Result<GroupId<TVertex, TInstance>> Register<TVertex, TInstance>(MeshId<TVertex> meshId, Id<RenderingSegment> segment)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        var groupId = GroupId.New<TVertex, TInstance>();
        logger.LogDebug("Creating {Instance} group {GroupId} of mesh {MeshId}", typeof(TInstance).Name, groupId, meshId);

        if (Result.Chain(
                () => meshManager.AddUse(meshId),
                () => groupBackend.CreateGroup(groupId, meshId).IfProblemUndo(() => meshManager.ReleaseUse(meshId)))
            .TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to create a group of mesh '{0}'", meshId);
        }

        _groups[groupId] = meshId;
        _owners.Add(segment, groupId);
        logger.LogInformation("Created {Instance} group {GroupId} of mesh {MeshId}", typeof(TInstance).Name, groupId, meshId);
        return groupId;
    }

    public DeletionResult Delete(GroupId groupId)
    {
        if (!_groups.TryGetValue(groupId, out var meshId))
        {
            return DeletionResult.NotFound();
        }

        if (_uses.Get(groupId) is var uses and > 0)
        {
            return new ResultProblem("Group '{0}' is still used by {1} consumer(s)", groupId, uses);
        }

        logger.LogDebug("Deleting group {GroupId}", groupId);
        _groups.Remove(groupId);
        _owners.Forget(groupId);

        // The mesh is released even when the backend fails, or it could never be deleted
        Result deleted = groupBackend.DeleteGroup(groupId).TryPickProblems(out var deleteProblems)
            ? deleteProblems.Prepend("Failed to delete group '{0}'", groupId)
            : Result.Success();
        if (Result.Concat([deleted, meshManager.ReleaseUse(meshId)]).TryPickProblems(out var problems))
        {
            return DeletionResult.Error(problems);
        }

        logger.LogInformation("Deleted group {GroupId}", groupId);
        return DeletionResult.Success();
    }

    internal Result AddUse(GroupId groupId)
    {
        if (!_groups.ContainsKey(groupId))
        {
            return new ResultProblem("Group '{0}' is not registered", groupId);
        }

        _uses.Add(groupId);
        return Result.Success();
    }

    internal Result ReleaseUse(GroupId groupId) =>
        _uses.TryRelease(groupId)
            ? Result.Success()
            : new ResultProblem("Group '{0}' has no use to release", groupId);

    public Result<InstanceId<TInstance>> AddInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (!IsRegistered(groupId))
        {
            return new ResultProblem("Group '{0}' is not registered", groupId);
        }

        var instanceId = InstanceId.New<TInstance>();
        return groupBackend.AddInstance(groupId, instanceId, instance).WithValueOnSuccess(instanceId);
    }

    public Result UpdateInstance<TVertex, TInstance>(
        GroupId<TVertex, TInstance> groupId,
        InstanceId<TInstance> instanceId,
        TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (!IsRegistered(groupId))
        {
            return new ResultProblem("Group '{0}' is not registered", groupId);
        }

        return groupBackend.UpdateInstance(groupId, instanceId, instance);
    }

    public DeletionResult RemoveInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (!IsRegistered(groupId))
        {
            return DeletionResult.Error(new ResultProblem("Group '{0}' is not registered", groupId));
        }

        return groupBackend.RemoveInstance(groupId, instanceId);
    }

    /// <summary>How many groups are registered.</summary>
    public int Count => _groups.Count;

    /// <summary>Deletes every group the segment was the last owner of.</summary>
    public Result DeleteSegment(Id<RenderingSegment> segment) => _owners.Release(segment, Delete);

    public void Dispose()
    {
        KeyValuePair<GroupId, MeshId>[] groups = [.. _groups];
        _groups.Clear();
        _uses.Clear();
        _owners.Clear();

        foreach (var (groupId, meshId) in groups)
        {
            var result = groupBackend.DeleteGroup(groupId);
            if (result.TryPickProblems(out var problems))
            {
                logger.LogError("Failed to delete group {GroupId}: {Problems}", groupId, problems);
            }
            else if (result.WasNotFound)
            {
                logger.LogWarning("Group {GroupId} was already gone from the group backend", groupId);
            }

            // The mesh manager may outlive this scope, so its meshes must not stay claimed.
            if (meshManager.ReleaseUse(meshId).TryPickProblems(out problems))
            {
                logger.LogError("Failed to release mesh {MeshId} of group {GroupId}: {Problems}", meshId, groupId, problems);
            }
        }
    }

    private bool IsRegistered(GroupId groupId) => _groups.ContainsKey(groupId);
}
