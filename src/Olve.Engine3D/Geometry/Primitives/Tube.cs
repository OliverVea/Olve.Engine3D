namespace Olve.Engine3D.Geometry.Primitives;

/// <summary>
/// A round tube along a path of a fixed number of points, validated by <see cref="Create"/>.
/// Its vertex and index counts depend only on the shape, so a tube whose path moves keeps its indices
/// and rebuilds only its vertices.
/// </summary>
public readonly record struct TubeShape
{
    private TubeShape(int pathPointCount, float radius, int ringSegments, bool capped)
    {
        PathPointCount = pathPointCount;
        Radius = radius;
        RingSegments = ringSegments;
        Capped = capped;
    }

    /// <summary>Points on the path; a curve is sampled to this many points.</summary>
    public int PathPointCount { get; }

    public float Radius { get; }

    /// <summary>Vertices round each ring.</summary>
    public int RingSegments { get; }

    /// <summary>Whether both ends are closed with a flat disc.</summary>
    public bool Capped { get; }

    public int VertexCount => PathPointCount * RingSegments + (Capped ? 2 * (RingSegments + 1) : 0);

    public int IndexCount => (PathPointCount - 1) * RingSegments * 6 + (Capped ? 2 * RingSegments * 3 : 0);

    /// <param name="pathPointCount">At least 2.</param>
    /// <param name="radius">Positive.</param>
    /// <param name="ringSegments">At least 3.</param>
    /// <param name="capped">Close both ends with a flat disc.</param>
    public static Result<TubeShape> Create(int pathPointCount, float radius, int ringSegments, bool capped = true)
    {
        if (pathPointCount < 2)
        {
            return new ResultProblem("A tube's path needs at least 2 points, got {0}", pathPointCount);
        }

        if (!(radius > 0) || !float.IsFinite(radius))
        {
            return new ResultProblem("A tube needs a positive, finite radius, got {0}", radius);
        }

        if (ringSegments < 3)
        {
            return new ResultProblem("A tube needs at least 3 ring segments, got {0}", ringSegments);
        }

        return new TubeShape(pathPointCount, radius, ringSegments, capped);
    }
}

/// <summary>
/// A round tube along a path, such as a cable. Rings follow parallel-transport frames, so the tube doesn't twist
/// round the path; consecutive points that coincide keep the previous direction.
/// Vertices: a ring per path point, then the start and end caps (a centre and a ring each).
/// Triangles wind counter-clockwise seen from outside, like <see cref="UnitCube"/>.
/// </summary>
public static class Tube
{
    /// <summary>Fills a tube's vertices along <paramref name="path"/>, which has <see cref="TubeShape.PathPointCount"/> points; allocates nothing.</summary>
    public static void Populate<T>(TubeShape shape, ReadOnlySpan<Vector3> path, Span<T> vertices)
        where T : struct, IWithPosition3D<T>, IWithNormal3D<T>
    {
        if (path.Length != shape.PathPointCount)
        {
            throw new ArgumentException($"The tube was shaped for {shape.PathPointCount} path points, got {path.Length}", nameof(path));
        }

        var segments = shape.RingSegments;
        var tangent = FirstTangent(path);
        var normal = Perpendicular(tangent);

        for (var i = 0; i < path.Length; i++)
        {
            var nextTangent = Tangent(path, i, tangent);
            normal = Transport(normal, tangent, nextTangent);
            tangent = nextTangent;
            var binormal = Vector3.Cross(tangent, normal);

            for (var j = 0; j < segments; j++)
            {
                var (sin, cos) = MathF.SinCos(float.Tau * j / segments);
                var direction = normal * cos + binormal * sin;
                var v = i * segments + j;
                vertices[v] = T.WithPosition(vertices[v], path[i] + direction * shape.Radius);
                vertices[v] = T.WithNormal(vertices[v], direction);
            }
        }

        if (!shape.Capped)
        {
            return;
        }

        var startTangent = FirstTangent(path);
        var start = path.Length * segments;
        var end = start + segments + 1;
        WriteCap(vertices, start, path[0], -startTangent, 0, segments);
        WriteCap(vertices, end, path[^1], tangent, (path.Length - 1) * segments, segments);
    }

    public static void GetVertices(TubeShape shape, ReadOnlySpan<Vector3> path, Span<Vector3> positions, Span<Vector3> normals)
    {
        var vertices = new PositionNormal[shape.VertexCount];
        Populate<PositionNormal>(shape, path, vertices);

        for (var i = 0; i < vertices.Length; i++)
        {
            positions[i] = vertices[i].Position;
            normals[i] = vertices[i].Normal;
        }
    }

    /// <summary>The indices depend only on the shape, so a tube that moves keeps them.</summary>
    public static void GetIndices(TubeShape shape, Span<uint> indices)
    {
        var segments = (uint)shape.RingSegments;
        var k = 0;

        for (var i = 0u; i < shape.PathPointCount - 1; i++)
        {
            var from = i * segments;
            var to = from + segments;
            for (var j = 0u; j < segments; j++)
            {
                var next = (j + 1) % segments;
                indices[k++] = from + j;
                indices[k++] = from + next;
                indices[k++] = to + next;
                indices[k++] = from + j;
                indices[k++] = to + next;
                indices[k++] = to + j;
            }
        }

        if (!shape.Capped)
        {
            return;
        }

        var start = (uint)shape.PathPointCount * segments;
        var end = start + segments + 1;
        for (var j = 0u; j < segments; j++)
        {
            var next = (j + 1) % segments;

            // The start cap faces back along the path, the end cap forward
            indices[k++] = start;
            indices[k++] = start + 1 + next;
            indices[k++] = start + 1 + j;
            indices[k++] = end;
            indices[k++] = end + 1 + j;
            indices[k++] = end + 1 + next;
        }
    }

    private static void WriteCap<T>(Span<T> vertices, int at, Vector3 centre, Vector3 normal, int ring, int segments)
        where T : struct, IWithPosition3D<T>, IWithNormal3D<T>
    {
        vertices[at] = T.WithPosition(vertices[at], centre);
        vertices[at] = T.WithNormal(vertices[at], normal);

        for (var j = 0; j < segments; j++)
        {
            var v = at + 1 + j;
            vertices[v] = T.WithPosition(vertices[v], vertices[ring + j].Position);
            vertices[v] = T.WithNormal(vertices[v], normal);
        }
    }

    /// <summary>The direction at the path's first point that goes somewhere; any axis for a path that stays put.</summary>
    private static Vector3 FirstTangent(ReadOnlySpan<Vector3> path)
    {
        for (var i = 1; i < path.Length; i++)
        {
            var step = path[i] - path[0];
            if (step.LengthSquared() > Epsilon * Epsilon)
            {
                return Vector3.Normalize(step);
            }
        }

        return Vector3.UnitZ;
    }

    /// <summary>The path's direction at point <paramref name="i"/>, across its neighbours; <paramref name="previous"/> where they coincide.</summary>
    private static Vector3 Tangent(ReadOnlySpan<Vector3> path, int i, Vector3 previous)
    {
        var across = path[int.Min(i + 1, path.Length - 1)] - path[int.Max(i - 1, 0)];
        return across.LengthSquared() > Epsilon * Epsilon ? Vector3.Normalize(across) : previous;
    }

    /// <summary>A unit vector perpendicular to <paramref name="tangent"/>, built from the axis least aligned with it.</summary>
    private static Vector3 Perpendicular(Vector3 tangent)
    {
        var a = Vector3.Abs(tangent);
        var axis = a.X <= a.Y && a.X <= a.Z ? Vector3.UnitX : a.Y <= a.Z ? Vector3.UnitY : Vector3.UnitZ;
        return Vector3.Normalize(Vector3.Cross(tangent, Vector3.Cross(axis, tangent)));
    }

    /// <summary>
    /// Carries <paramref name="normal"/> from one tangent to the next by the smallest rotation between them
    /// (parallel transport); a reversal keeps the normal, which is still perpendicular.
    /// </summary>
    private static Vector3 Transport(Vector3 normal, Vector3 from, Vector3 to)
    {
        var axis = Vector3.Cross(from, to);
        var sin = axis.Length();
        var cos = Vector3.Dot(from, to);

        if (sin < Epsilon)
        {
            return normal;
        }

        var rotation = Quaternion.CreateFromAxisAngle(axis / sin, MathF.Atan2(sin, cos));
        var carried = Vector3.Transform(normal, rotation);

        // Keep the frame orthonormal against drift over long paths
        return Vector3.Normalize(carried - to * Vector3.Dot(carried, to));
    }

    private struct PositionNormal : IWithPosition3D<PositionNormal>, IWithNormal3D<PositionNormal>
    {
        public Vector3 Position { get; private init; }
        public Vector3 Normal { get; private init; }

        public static PositionNormal WithPosition(PositionNormal self, Vector3 position) => self with { Position = position };
        public static PositionNormal WithNormal(PositionNormal self, Vector3 normal) => self with { Normal = normal };
    }
}
