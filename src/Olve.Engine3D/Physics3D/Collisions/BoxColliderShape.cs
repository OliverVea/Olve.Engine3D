using Olve.Engine3D.Math;

namespace Olve.Engine3D.Physics3D.Collisions;

// TODO: CapsuleColliderShape — cylinder + hemisphere caps, useful for character controllers
// TODO: MeshColliderShape — per-triangle raycast against arbitrary mesh geometry

public class BoxColliderShape(Vector3 halfExtents) : IColliderShape
{
    public Vector3 HalfExtents { get; } = halfExtents;
    private readonly AABB _localAABB = new(-halfExtents, halfExtents);

    public AABB GetWorldAABB(in Matrix4x4 worldMatrix)
    {
        return AABBHelper.TransformAABB(_localAABB, worldMatrix);
    }

    public bool TryRaycast(
        in Ray ray,
        in Matrix4x4 worldMatrix,
        out float distance,
        out Vector3 hitPosition)
    {
        // Narrow phase: transform ray to local space for OBB test
        if (!Matrix4x4.Invert(worldMatrix, out var inverseMatrix))
        {
            distance = default;
            hitPosition = default;
            return false;
        }

        var localOrigin = Vector3.Transform(ray.Origin, inverseMatrix);
        var localTarget = Vector3.Transform(ray.Origin + ray.Direction, inverseMatrix);
        var localDirection = localTarget - localOrigin;
        var localRay = new Ray(localOrigin, localDirection);

        if (!_localAABB.TryIntersectRay(localRay, out var tLocal))
        {
            distance = default;
            hitPosition = default;
            return false;
        }

        // Convert local-space t back to world-space distance
        var localHitPoint = localOrigin + tLocal * localDirection;
        var worldHitPoint = Vector3.Transform(localHitPoint, worldMatrix);
        distance = (worldHitPoint - ray.Origin).Length();
        hitPosition = worldHitPoint;
        return true;
    }
}
