using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Geometry;

namespace Olve.Engine3D.Math;

public static class AABBHelper
{
    public static Result<AABB> ComputeAABBOfMesh(MeshData meshData, float scale = 1f)
        => ComputeAABBOfMesh(meshData, new Vector3(scale, scale, scale));

    public static Result<AABB> ComputeAABBOfMesh(MeshData meshData, Vector3 scale)
    {
        if (meshData.Positions.Length == 0)
        {
            return new ResultProblem("Mesh has no vertices");
        }

        if (scale.X == 0f || scale.Y == 0f || scale.Z == 0f)
        {
            return new ResultProblem("Scale cannot be zero");
        }

        var unscaled = FromPoints(meshData.Positions);
        var a = unscaled.Min * scale;
        var b = unscaled.Max * scale;

        return new AABB(Vector3.Min(a, b), Vector3.Max(a, b));
    }

    public static Result<AABB> ComputeAABBOfVertices<TVertex>(ReadOnlySpan<TVertex> vertices)
        where TVertex : IWithPosition3D<TVertex>
    {
        if (vertices.IsEmpty)
        {
            return new ResultProblem("Mesh has no vertices");
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var vertex in vertices)
        {
            min = Vector3.Min(min, vertex.Position);
            max = Vector3.Max(max, vertex.Position);
        }

        return new AABB(min, max);
    }

    public static AABB FromPoints(ReadOnlySpan<Vector3> points)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        foreach (var point in points)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return new AABB(min, max);
    }

    public static AABB TransformAABB(in AABB localAABB, in Matrix4x4 worldMatrix)
    {
        Span<Vector3> corners =
        [
            new(localAABB.Min.X, localAABB.Min.Y, localAABB.Min.Z),
            new(localAABB.Max.X, localAABB.Min.Y, localAABB.Min.Z),
            new(localAABB.Min.X, localAABB.Max.Y, localAABB.Min.Z),
            new(localAABB.Max.X, localAABB.Max.Y, localAABB.Min.Z),
            new(localAABB.Min.X, localAABB.Min.Y, localAABB.Max.Z),
            new(localAABB.Max.X, localAABB.Min.Y, localAABB.Max.Z),
            new(localAABB.Min.X, localAABB.Max.Y, localAABB.Max.Z),
            new(localAABB.Max.X, localAABB.Max.Y, localAABB.Max.Z),
        ];

        foreach (ref var corner in corners)
        {
            corner = Vector3.Transform(corner, worldMatrix);
        }

        return FromPoints(corners);
    }

    public static Result<float> GetUniformScaleToFitInside(MeshData meshData, AABB target)
    {
        if (GetHeterogeneousScaleToFitInside(meshData, target).TryPickProblems(out var problems, out var heterogeneousScale))
        {
            return problems.Prepend("Failed to compute AABB of mesh");
        }

        return float.Min(heterogeneousScale.X, float.Min(heterogeneousScale.Y, heterogeneousScale.Z));
    }

    public static Result<Vector3> GetHeterogeneousScaleToFitInside(MeshData meshData, AABB target)
    {
        if (ComputeAABBOfMesh(meshData).TryPickProblems(out var problems, out var aabb))
        {
            return problems.Prepend("Failed to compute AABB of mesh");
        }

        var deltaX = aabb.Max.X - aabb.Min.X;
        var deltaY = aabb.Max.Y - aabb.Min.Y;
        var deltaZ = aabb.Max.Z - aabb.Min.Z;

        if (deltaX == 0f || deltaY == 0f || deltaZ == 0f)
        {
            return new ResultProblem("Mesh AABB has zero extent along one or more axes.");
        }

        var scaleX = (target.Max.X - target.Min.X) / (aabb.Max.X - aabb.Min.X);
        var scaleY = (target.Max.Y - target.Min.Y) / (aabb.Max.Y - aabb.Min.Y);
        var scaleZ = (target.Max.Z - target.Min.Z) / (aabb.Max.Z - aabb.Min.Z);

        return new Vector3(scaleX, scaleY, scaleZ);
    }
}