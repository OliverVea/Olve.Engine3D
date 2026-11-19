using Olve.Engine3D.Rendering.Meshes;

namespace Olve.Engine3D.Rendering.Instancing;

/// <summary>Creates and deletes instance groups on the GPU and keeps their instances.</summary>
public interface IGroupBackend
{
    Result CreateGroup<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, MeshId<TVertex> meshId)
        where TVertex : IVertexData
        where TInstance : IInstanceData;

    DeletionResult DeleteGroup(GroupId groupId);

    Result AddInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData;

    Result UpdateInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId, TInstance instance)
        where TVertex : IVertexData
        where TInstance : IInstanceData;

    DeletionResult RemoveInstance<TVertex, TInstance>(GroupId<TVertex, TInstance> groupId, InstanceId<TInstance> instanceId)
        where TVertex : IVertexData
        where TInstance : IInstanceData;
}
