namespace Olve.Engine3D.Math.Splines;

/// <summary>
/// A characteristic matrix applied to four control values gives four coefficients, one per power of t; a point on the
/// segment is <c>t.X * c[0] + t.Y * c[1] + t.Z * c[2] + t.W * c[3]</c>.
/// </summary>
internal static class SplineCoefficients
{
    public const int PerSegment = 4;

    public static void Compute(in Matrix4x4 m, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Span<Vector2> c)
    {
        c[0] = m.M11 * p0 + m.M12 * p1 + m.M13 * p2 + m.M14 * p3;
        c[1] = m.M21 * p0 + m.M22 * p1 + m.M23 * p2 + m.M24 * p3;
        c[2] = m.M31 * p0 + m.M32 * p1 + m.M33 * p2 + m.M34 * p3;
        c[3] = m.M41 * p0 + m.M42 * p1 + m.M43 * p2 + m.M44 * p3;
    }

    public static void Compute(in Matrix4x4 m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Span<Vector3> c)
    {
        c[0] = m.M11 * p0 + m.M12 * p1 + m.M13 * p2 + m.M14 * p3;
        c[1] = m.M21 * p0 + m.M22 * p1 + m.M23 * p2 + m.M24 * p3;
        c[2] = m.M31 * p0 + m.M32 * p1 + m.M33 * p2 + m.M34 * p3;
        c[3] = m.M41 * p0 + m.M42 * p1 + m.M43 * p2 + m.M44 * p3;
    }

    public static void Compute(in Matrix4x4 m, Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3, Span<Vector4> c)
    {
        c[0] = m.M11 * p0 + m.M12 * p1 + m.M13 * p2 + m.M14 * p3;
        c[1] = m.M21 * p0 + m.M22 * p1 + m.M23 * p2 + m.M24 * p3;
        c[2] = m.M31 * p0 + m.M32 * p1 + m.M33 * p2 + m.M34 * p3;
        c[3] = m.M41 * p0 + m.M42 * p1 + m.M43 * p2 + m.M44 * p3;
    }

    public static Vector2 Evaluate(in Vector4 t, ReadOnlySpan<Vector2> c) => t.X * c[0] + t.Y * c[1] + t.Z * c[2] + t.W * c[3];

    public static Vector3 Evaluate(in Vector4 t, ReadOnlySpan<Vector3> c) => t.X * c[0] + t.Y * c[1] + t.Z * c[2] + t.W * c[3];

    public static Vector4 Evaluate(in Vector4 t, ReadOnlySpan<Vector4> c) => t.X * c[0] + t.Y * c[1] + t.Z * c[2] + t.W * c[3];
}
