namespace Olve.Engine3D.Geometry.Primitives;

/// <summary>Which ends of a lathe profile are closed with a flat disc.</summary>
[Flags]
public enum LatheCaps
{
    None = 0,
    Start = 1,
    End = 2,
    Both = Start | End,
}

/// <summary>
/// A 2D profile to turn round the Y axis, validated by <see cref="Create"/>.
/// Each profile point is (radius, height); walked in order, the outside of the surface is on the right,
/// so a profile running bottom to top faces away from the axis.
/// </summary>
public readonly record struct LatheShape
{
    private LatheShape(Vector2[] profile, int segments, LatheCaps caps, float creaseAngle, bool[] creases)
    {
        Profile = profile;
        Segments = segments;
        Caps = caps;
        CreaseAngle = creaseAngle;
        Creases = creases;
    }

    /// <summary>The profile, including the points on the axis that close a capped end.</summary>
    public IReadOnlyList<Vector2> Profile { get; }

    /// <summary>Vertices round the axis in each ring.</summary>
    public int Segments { get; }

    public LatheCaps Caps { get; }

    /// <summary>Profile corners that turn by more than this many radians are sharp: their normals split.</summary>
    public float CreaseAngle { get; }

    /// <summary>Whether each profile point is a sharp corner, with a ring of vertices for each side.</summary>
    internal IReadOnlyList<bool> Creases { get; }

    /// <summary>Rings of vertices: one per profile point, two at a sharp corner.</summary>
    public int RingCount => Profile.Count + Creases.Count(c => c);

    public int VertexCount => RingCount * Segments;

    public int IndexCount => (Profile.Count - 1) * Segments * 6;

    /// <param name="profile">At least two (radius, height) points, radii not negative, no two consecutive points equal.</param>
    /// <param name="segments">Vertices round the axis, at least 3.</param>
    /// <param name="caps">Ends to close with a disc; an end already on the axis needs none and gets none.</param>
    /// <param name="creaseAngle">Corners turning by more than this many radians get split normals; between 0 and pi.</param>
    public static Result<LatheShape> Create(ReadOnlySpan<Vector2> profile, int segments, LatheCaps caps = LatheCaps.None, float creaseAngle = float.Pi / 6)
    {
        if (profile.Length < 2)
        {
            return new ResultProblem("A lathe profile needs at least 2 points, got {0}", profile.Length);
        }

        if (segments < 3)
        {
            return new ResultProblem("A lathe needs at least 3 segments, got {0}", segments);
        }

        if (!(creaseAngle >= 0 && creaseAngle <= float.Pi))
        {
            return new ResultProblem("A lathe's crease angle must be between 0 and pi, got {0}", creaseAngle);
        }

        for (var i = 0; i < profile.Length; i++)
        {
            if (profile[i].X < 0 || !float.IsFinite(profile[i].X) || !float.IsFinite(profile[i].Y))
            {
                return new ResultProblem("Lathe profile point {0} must be finite with a radius of at least 0, got {1}", i, profile[i]);
            }

            if (i > 0 && profile[i] == profile[i - 1])
            {
                return new ResultProblem("Lathe profile points {0} and {1} are the same point {2}", i - 1, i, profile[i]);
            }
        }

        var points = new List<Vector2>(profile.Length + 2);
        var forced = new List<bool>(profile.Length + 2);

        var capStart = caps.HasFlag(LatheCaps.Start) && profile[0].X > 0;
        var capEnd = caps.HasFlag(LatheCaps.End) && profile[^1].X > 0;

        if (capStart)
        {
            points.Add(profile[0] with { X = 0 });
            forced.Add(false);
        }

        for (var i = 0; i < profile.Length; i++)
        {
            points.Add(profile[i]);
            forced.Add((capStart && i == 0) || (capEnd && i == profile.Length - 1));
        }

        if (capEnd)
        {
            points.Add(profile[^1] with { X = 0 });
            forced.Add(false);
        }

        var creases = new bool[points.Count];
        for (var i = 1; i < points.Count - 1; i++)
        {
            var incoming = Vector2.Normalize(points[i] - points[i - 1]);
            var outgoing = Vector2.Normalize(points[i + 1] - points[i]);
            var turn = MathF.Acos(float.Clamp(Vector2.Dot(incoming, outgoing), -1, 1));
            creases[i] = forced[i] || turn > creaseAngle;
        }

        return new LatheShape(points.ToArray(), segments, caps, creaseAngle, creases);
    }
}

/// <summary>
/// A surface of revolution: a <see cref="LatheShape"/>'s profile turned round the Y axis.
/// Normals are smooth round the axis and along the profile, and split at its sharp corners.
/// Triangles wind counter-clockwise seen from outside, like <see cref="UnitCube"/>.
/// </summary>
public static class Lathe
{
    public static void GetVertices(LatheShape shape, Span<Vector3> positions, Span<Vector3> normals)
    {
        var profile = shape.Profile;
        var last = profile.Count - 1;
        var v = 0;

        for (var i = 0; i <= last; i++)
        {
            var incoming = i > 0 ? SegmentNormal(profile[i - 1], profile[i]) : (Vector2?)null;
            var outgoing = i < last ? SegmentNormal(profile[i], profile[i + 1]) : (Vector2?)null;

            if (shape.Creases[i])
            {
                WriteRing(shape, profile[i], incoming!.Value, positions, normals, ref v);
                WriteRing(shape, profile[i], outgoing!.Value, positions, normals, ref v);
                continue;
            }

            var normal = (incoming, outgoing) switch
            {
                ({ } a, { } b) => Vector2.Normalize(a + b),
                ({ } a, null) => a,
                (null, { } b) => b,
                _ => throw new InvalidOperationException("A lathe profile has at least two points"),
            };

            // A smooth surface crossing the axis faces straight along it
            if (profile[i].X == 0 && (i == 0 || i == last))
            {
                normal = new Vector2(0, MathF.CopySign(1, normal.Y));
            }

            WriteRing(shape, profile[i], normal, positions, normals, ref v);
        }
    }

    public static void Populate<T>(LatheShape shape, Span<T> vertices)
        where T : struct, IWithPosition3D<T>, IWithNormal3D<T>
    {
        var positions = new Vector3[shape.VertexCount];
        var normals = new Vector3[shape.VertexCount];
        GetVertices(shape, positions, normals);

        for (var i = 0; i < positions.Length; i++)
        {
            vertices[i] = T.WithPosition(vertices[i], positions[i]);
            vertices[i] = T.WithNormal(vertices[i], normals[i]);
        }
    }

    public static void GetIndices(LatheShape shape, Span<uint> indices)
    {
        var segments = (uint)shape.Segments;
        var ring = 0u;
        var k = 0;

        for (var i = 0; i < shape.Profile.Count - 1; i++)
        {
            // The ring leaving point i is its second ring at a sharp corner
            if (shape.Creases[i])
            {
                ring++;
            }

            var from = ring * segments;
            var to = (ring + 1) * segments;

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

            ring++;
        }
    }

    /// <summary>The outward normal of a profile segment: the right of its direction.</summary>
    private static Vector2 SegmentNormal(Vector2 from, Vector2 to)
    {
        var d = Vector2.Normalize(to - from);
        return new Vector2(d.Y, -d.X);
    }

    private static void WriteRing(LatheShape shape, Vector2 point, Vector2 normal, Span<Vector3> positions, Span<Vector3> normals, ref int v)
    {
        for (var j = 0; j < shape.Segments; j++)
        {
            var angle = float.Tau * j / shape.Segments;
            var (sin, cos) = MathF.SinCos(angle);
            positions[v] = new Vector3(point.X * cos, point.Y, -point.X * sin);
            normals[v] = new Vector3(normal.X * cos, normal.Y, -normal.X * sin);
            v++;
        }
    }
}
