namespace Olve.Engine3D.Geometry.Primitives;

/// <summary>A box centred on the origin with rounded edges and corners, validated by <see cref="Create"/>.</summary>
public readonly record struct BevelledBoxShape
{
    private BevelledBoxShape(Vector3 size, float bevelRadius, int bevelSegments)
    {
        Size = size;
        BevelRadius = bevelRadius;
        BevelSegments = bevelSegments;
    }

    public Vector3 Size { get; }

    /// <summary>The radius the edges are rounded to.</summary>
    public float BevelRadius { get; }

    /// <summary>Flat steps across each rounded edge.</summary>
    public int BevelSegments { get; }

    /// <summary>Grid points along each side of a face: the face's half of the bevel at both ends.</summary>
    internal int FaceSide => BevelSegments + 2;

    public int VertexCount => 6 * FaceSide * FaceSide;

    public int IndexCount => 6 * (FaceSide - 1) * (FaceSide - 1) * 6;

    /// <param name="size">The full extent along each axis, all positive.</param>
    /// <param name="bevelRadius">Above 0 and at most half the smallest extent.</param>
    /// <param name="bevelSegments">Flat steps across each rounded edge: an even count, at least 2, since each face takes half.</param>
    public static Result<BevelledBoxShape> Create(Vector3 size, float bevelRadius, int bevelSegments)
    {
        if (!(size.X > 0 && size.Y > 0 && size.Z > 0) || !float.IsFinite(size.X + size.Y + size.Z))
        {
            return new ResultProblem("A bevelled box needs a positive, finite size, got {0}", size);
        }

        var smallestHalf = MathF.Min(size.X, MathF.Min(size.Y, size.Z)) / 2;
        if (!(bevelRadius > 0 && bevelRadius <= smallestHalf))
        {
            return new ResultProblem("A bevelled box's bevel radius must be above 0 and at most {0}, half its smallest extent, got {1}", smallestHalf, bevelRadius);
        }

        if (bevelSegments < 2 || bevelSegments % 2 != 0)
        {
            return new ResultProblem("A bevelled box needs an even number of bevel segments, at least 2, got {0}", bevelSegments);
        }

        return new BevelledBoxShape(size, bevelRadius, bevelSegments);
    }
}

/// <summary>
/// A box with rounded edges and corners: flat faces joined by bevels of a <see cref="BevelledBoxShape"/>'s radius,
/// with normals smooth across the bevels. Six face grids; each covers its face and half of the bevels round it.
/// Triangles wind counter-clockwise seen from outside, like <see cref="UnitCube"/>.
/// </summary>
public static class BevelledBox
{
    // Each face: its normal, and the two in-face axes with normal = cross(u, v), so the grid winds outward
    private static readonly (Vector3 Normal, Vector3 U, Vector3 V)[] Faces =
    [
        (Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ),
        (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX),
        (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY),
        (-Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX),
    ];

    public static void GetVertices(BevelledBoxShape shape, Span<Vector3> positions, Span<Vector3> normals)
    {
        var half = shape.Size / 2;
        var inner = half - new Vector3(shape.BevelRadius);
        var side = shape.FaceSide;

        // Offsets across a face, as tangents of the bevel angle: the face's half of the bevel runs 0 to 45 degrees
        var halfSteps = shape.BevelSegments / 2;
        Span<float> offsets = stackalloc float[side];
        for (var k = 0; k <= halfSteps; k++)
        {
            var t = k == halfSteps ? 1f : MathF.Tan(float.Pi / 4 * k / halfSteps);
            offsets[halfSteps - k] = -t;
            offsets[halfSteps + 1 + k] = t;
        }

        var v = 0;
        foreach (var (normal, u, w) in Faces)
        {
            for (var a = 0; a < side; a++)
            {
                for (var b = 0; b < side; b++)
                {
                    // A point on the inner box (the box shrunk by the radius), pushed out along the normal there
                    var core = normal * inner
                               + u * (a > halfSteps ? 1 : -1) * Vector3.Dot(inner, u)
                               + w * (b > halfSteps ? 1 : -1) * Vector3.Dot(inner, w);
                    var direction = Vector3.Normalize(normal + u * offsets[a] + w * offsets[b]);
                    positions[v] = core + direction * shape.BevelRadius;
                    normals[v] = direction;
                    v++;
                }
            }
        }
    }

    public static void Populate<T>(BevelledBoxShape shape, Span<T> vertices)
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

    public static void GetIndices(BevelledBoxShape shape, Span<uint> indices)
    {
        var side = (uint)shape.FaceSide;
        var k = 0;

        for (var face = 0u; face < 6; face++)
        {
            var start = face * side * side;
            for (var a = 0u; a < side - 1; a++)
            {
                for (var b = 0u; b < side - 1; b++)
                {
                    var i00 = start + a * side + b;
                    var i10 = i00 + side;
                    var i01 = i00 + 1;
                    var i11 = i10 + 1;
                    indices[k++] = i00;
                    indices[k++] = i10;
                    indices[k++] = i11;
                    indices[k++] = i00;
                    indices[k++] = i11;
                    indices[k++] = i01;
                }
            }
        }
    }
}
