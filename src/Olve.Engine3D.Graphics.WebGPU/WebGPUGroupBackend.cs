using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Groups as their mesh, the layouts their pipelines read, and an instance buffer.</summary>
internal sealed class WebGPUGroupBackend(WebGPUResources resources) : IGroupBackend
{
    public Result CreateGroup<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, MeshId<TVertex> meshId)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (resources.Groups.ContainsKey(groupId))
        {
            return new ResultProblem("Group '{0}' already exists", groupId);
        }

        if (!resources.Meshes.ContainsKey(meshId))
        {
            return new ResultProblem("Mesh '{0}' has no WebGPU buffers", meshId);
        }

        resources.Groups[groupId] = new WgpuGroup(meshId, TVertex.Layout, TInstance.Layout, TInstance.FloatCount);
        return Result.Success();
    }

    public DeletionResult DeleteGroup(GroupId groupId)
    {
        if (!resources.Groups.Remove(groupId, out var group))
        {
            return DeletionResult.NotFound();
        }

        group.Release();
        return DeletionResult.Success();
    }

    public Result AddInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (!resources.Groups.TryGetValue(groupId, out var group))
        {
            return new ResultProblem("Group '{0}' has no WebGPU state", groupId);
        }

        return group.Add(instanceId, instance);
    }

    public Result UpdateInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (!resources.Groups.TryGetValue(groupId, out var group))
        {
            return new ResultProblem("Group '{0}' has no WebGPU state", groupId);
        }

        return group.Update(instanceId, instance);
    }

    public DeletionResult RemoveInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId)
        where TVertex : IVertexData
        where TInstance : IInstanceData
    {
        if (!resources.Groups.TryGetValue(groupId, out var group))
        {
            return new ResultProblem("Group '{0}' has no WebGPU state", groupId);
        }

        return group.Remove(instanceId) ? DeletionResult.Success() : DeletionResult.NotFound();
    }
}
