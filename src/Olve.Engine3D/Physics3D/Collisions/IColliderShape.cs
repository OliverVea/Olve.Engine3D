using Olve.Engine3D.Math;

namespace Olve.Engine3D.Physics3D.Collisions;

public interface IColliderShape
{
    AABB GetWorldAABB(in Matrix4x4 worldMatrix);

    bool TryRaycast(
        in Ray ray,
        in Matrix4x4 worldMatrix,
        out float distance,
        out Vector3 hitPosition);
}
