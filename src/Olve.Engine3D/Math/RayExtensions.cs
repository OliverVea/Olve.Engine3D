using System.Diagnostics.CodeAnalysis;

namespace Olve.Engine3D.Math;

public static class RayExtensions
{
    /// <summary>
    /// Where along the ray, in multiples of its direction, it reaches height <paramref name="y"/>; false when the ray is horizontal.
    /// </summary>
    public static bool TryGetDistanceToY(this Ray ray, float y, out float distance)
    {
        if (float.Abs(ray.Direction.Y) < Epsilon)
        {
            distance = default;
            return false;
        }

        distance = (y - ray.Origin.Y) / ray.Direction.Y;
        return true;
    }

    public static bool TryEvaluateWithY(this Ray ray, float y, [MaybeNullWhen(false)] out Vector3 point)
    {
        if (!ray.TryGetDistanceToY(y, out var t))
        {
            point = default;
            return false;
        }

        point = new Vector3(
            ray.Origin.X + t * ray.Direction.X,
            y,
            ray.Origin.Z + t * ray.Direction.Z);

        return true;
    }

    /// <summary>
    /// Where along the ray, in multiples of its direction, it crosses <paramref name="plane"/>, from either side;
    /// false when the ray runs parallel to the plane or the plane is behind its origin.
    /// Unlike <see cref="TryGetDistanceToY"/>, a crossing behind the origin is no hit.
    /// </summary>
    public static bool TryGetDistanceToPlane(this Ray ray, Plane plane, out float distance)
    {
        var approach = Vector3.Dot(plane.Normal, ray.Direction);
        var scale = plane.Normal.Length() * ray.Direction.Length();
        if (float.Abs(approach) <= Epsilon * scale)
        {
            distance = default;
            return false;
        }

        distance = -(Vector3.Dot(plane.Normal, ray.Origin) + plane.D) / approach;
        return distance >= 0;
    }

    /// <summary>The point where the ray crosses <paramref name="plane"/>; false as for <see cref="TryGetDistanceToPlane"/>.</summary>
    public static bool TryIntersectPlane(this Ray ray, Plane plane, out Vector3 point)
    {
        if (!ray.TryGetDistanceToPlane(plane, out var t))
        {
            point = default;
            return false;
        }

        point = ray.Origin + t * ray.Direction;
        return true;
    }
}
