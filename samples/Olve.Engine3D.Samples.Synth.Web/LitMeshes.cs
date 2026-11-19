using Olve.Engine3D.Geometry.Primitives;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Generated.Shaders;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>Mesh buffers of the room's lit shapes, from the engine's primitives.</summary>
public static class LitMeshes
{
    public static Result<MeshBuffers<Shaders.Shared.LitVertex>> Lathe(ReadOnlySpan<System.Numerics.Vector2> profile, int segments, LatheCaps caps = LatheCaps.None)
    {
        if (LatheShape.Create(profile, segments, caps).TryPickProblems(out var problems, out var shape))
        {
            return problems;
        }

        var vertices = new Shaders.Shared.LitVertex[shape.VertexCount];
        var indices = new uint[shape.IndexCount];
        Geometry.Primitives.Lathe.Populate<Shaders.Shared.LitVertex>(shape, vertices);
        Geometry.Primitives.Lathe.GetIndices(shape, indices);
        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    public static Result<MeshBuffers<Shaders.Shared.LitVertex>> Box(System.Numerics.Vector3 size, float bevel, int segments = 4)
    {
        if (BevelledBoxShape.Create(size, bevel, segments).TryPickProblems(out var problems, out var shape))
        {
            return problems;
        }

        var vertices = new Shaders.Shared.LitVertex[shape.VertexCount];
        var indices = new uint[shape.IndexCount];
        BevelledBox.Populate<Shaders.Shared.LitVertex>(shape, vertices);
        BevelledBox.GetIndices(shape, indices);
        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    /// <summary>
    /// A thumb wheel on an axle along x: a tyre of <paramref name="radius"/> and <paramref name="width"/>, its edges rounded
    /// over by <paramref name="rounding"/>, with <paramref name="ribs"/> grip ridges across its tread, each standing
    /// <paramref name="ribDepth"/> proud, and flat sides.
    /// </summary>
    public static MeshBuffers<Shaders.Shared.LitVertex> RibbedWheel(float radius, float width, int ribs, float ribDepth, float rounding)
    {
        const int Steps = 8;
        var around = ribs * 6;
        // The profile across the tread: x along the axle, and how far in from the full radius, rounding over each edge
        var profile = new List<(float X, float Inset, float Grip)>();
        for (var i = 0; i <= Steps; i++)
        {
            var angle = MathF.PI / 2 * (1 - i / (float)Steps);
            profile.Add((-width / 2 + rounding * (1 - MathF.Cos(angle)), rounding * (1 - MathF.Sin(angle)), i / (float)Steps));
        }

        foreach (var (x, inset, grip) in profile.AsEnumerable().Reverse().ToList())
        {
            profile.Add((-x, inset, grip));
        }

        var rows = profile.Count;
        var grid = new System.Numerics.Vector3[rows, around + 1];
        for (var r = 0; r < rows; r++)
        {
            for (var a = 0; a <= around; a++)
            {
                var theta = 2 * MathF.PI * a / around;
                // Ridges with flat-topped crests, fading out over the rounded edges
                var crest = MathF.Pow(0.5f + 0.5f * MathF.Cos(ribs * theta), 3);
                var reach = radius - profile[r].Inset - ribDepth * (1 - crest * profile[r].Grip);
                grid[r, a] = new System.Numerics.Vector3(profile[r].X, reach * MathF.Cos(theta), reach * MathF.Sin(theta));
            }
        }

        var vertices = new List<Shaders.Shared.LitVertex>();
        var indices = new List<uint>();
        for (var r = 0; r < rows; r++)
        {
            for (var a = 0; a <= around; a++)
            {
                var along = grid[r, (a + 1) % around] - grid[r, (a + around - 1) % around];
                var across = grid[int.Min(r + 1, rows - 1), a] - grid[int.Max(r - 1, 0), a];
                var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(along, across));
                // Outward, away from the axle
                var outward = grid[r, a] with { X = 0 };
                vertices.Add(new Shaders.Shared.LitVertex(grid[r, a], System.Numerics.Vector3.Dot(normal, outward) < 0 ? -normal : normal));
            }
        }

        for (var r = 0; r < rows - 1; r++)
        {
            for (var a = 0; a < around; a++)
            {
                var at = (uint)(r * (around + 1) + a);
                var next = at + (uint)(around + 1);
                indices.AddRange([at, next, at + 1, at + 1, next, next + 1]);
            }
        }

        // The flat sides, as fans from the axle
        foreach (var (row, side) in new[] { (0, -1f), (rows - 1, 1f) })
        {
            var centre = (uint)vertices.Count;
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(grid[row, 0].X, 0, 0), new System.Numerics.Vector3(side, 0, 0)));
            for (var a = 0; a <= around; a++)
            {
                vertices.Add(new Shaders.Shared.LitVertex(grid[row, a], new System.Numerics.Vector3(side, 0, 0)));
            }

            for (var a = 0; a < around; a++)
            {
                var first = centre + 1 + (uint)a;
                indices.AddRange(side > 0 ? [centre, first, first + 1] : [centre, first + 1, first]);
            }
        }

        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    /// <summary>
    /// A plate of <paramref name="width"/> by <paramref name="height"/> (x right, y up) with a round hole through its
    /// middle, its face <paramref name="thickness"/> out along z and its outer edges walled down to z = 0.
    /// </summary>
    public static MeshBuffers<Shaders.Shared.LitVertex> PlateWithHole(float width, float height, float radius, float thickness)
    {
        const int Segments = 96;
        var vertices = new List<Shaders.Shared.LitVertex>();
        var indices = new List<uint>();
        var half = new System.Numerics.Vector2(width / 2, height / 2);
        for (var i = 0; i <= Segments; i++)
        {
            var theta = 2 * MathF.PI * i / Segments;
            var direction = new System.Numerics.Vector2(MathF.Cos(theta), MathF.Sin(theta));
            // Out along the ray to the plate's edge
            var reach = MathF.Min(MathF.Abs(direction.X) > 1e-6f ? half.X / MathF.Abs(direction.X) : float.MaxValue,
                MathF.Abs(direction.Y) > 1e-6f ? half.Y / MathF.Abs(direction.Y) : float.MaxValue);
            var inner = direction * radius;
            var outer = direction * reach;
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(inner, thickness), System.Numerics.Vector3.UnitZ));
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(outer, thickness), System.Numerics.Vector3.UnitZ));
        }

        for (var i = 0; i < Segments; i++)
        {
            var at = (uint)(2 * i);
            indices.AddRange([at, at + 1, at + 2, at + 2, at + 1, at + 3]);
        }

        // The ray meets the corners only between segments, so each corner gets a triangle of its own
        var centreCorners = new[] { new System.Numerics.Vector2(1, 1), new(-1, 1), new(-1, -1), new(1, -1) };
        foreach (var corner in centreCorners)
        {
            var point = corner * half;
            var angle = MathF.Atan2(point.Y, point.X);
            var segment = (int)MathF.Floor((angle < 0 ? angle + 2 * MathF.PI : angle) / (2 * MathF.PI) * Segments);
            var at = (uint)vertices.Count;
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(point, thickness), System.Numerics.Vector3.UnitZ));
            indices.AddRange([(uint)(2 * segment + 1), at, (uint)(2 * segment + 3)]);
        }

        // The walls round the outer edge
        System.Numerics.Vector2[] rim = [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)];
        for (var i = 0; i < 4; i++)
        {
            var a = rim[i];
            var b = rim[(i + 1) % 4];
            var edge = b - a;
            var normal = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(edge.Y, -edge.X, 0));
            var at = (uint)vertices.Count;
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(a, 0), normal));
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(b, 0), normal));
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(a, thickness), normal));
            vertices.Add(new Shaders.Shared.LitVertex(new System.Numerics.Vector3(b, thickness), normal));
            indices.AddRange([at, at + 1, at + 2, at + 2, at + 1, at + 3]);
        }

        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }

    /// <summary>
    /// A lathe round y whose rings can be fluted: each ring of the profile has a radius and height, and how deep and how
    /// many grooves run up it there (0 deep for a smooth ring). Grip for knob skirts and knurled plug barrels. The normals
    /// come from the surface's neighbours, so the flutes catch the light.
    /// </summary>
    public static MeshBuffers<Shaders.Shared.LitVertex> FlutedLathe(IReadOnlyList<(System.Numerics.Vector2 Point, float Depth, int Flutes)> rings, int around)
    {
        var rows = rings.Count;
        var grid = new System.Numerics.Vector3[rows, around + 1];
        for (var r = 0; r < rows; r++)
        {
            var (point, depth, flutes) = rings[r];
            for (var a = 0; a <= around; a++)
            {
                var theta = 2 * MathF.PI * a / around;
                // Flat-topped ridges between round grooves
                var ridge = flutes > 0 ? MathF.Pow(0.5f + 0.5f * MathF.Cos(flutes * theta), 0.5f) : 1;
                var reach = point.X - depth * (1 - ridge);
                grid[r, a] = new System.Numerics.Vector3(reach * MathF.Cos(theta), point.Y, reach * MathF.Sin(theta));
            }
        }

        var vertices = new List<Shaders.Shared.LitVertex>();
        var indices = new List<uint>();
        for (var r = 0; r < rows; r++)
        {
            for (var a = 0; a <= around; a++)
            {
                var along = grid[r, (a + 1) % around] - grid[r, (a + around - 1) % around];
                var up = grid[int.Min(r + 1, rows - 1), a] - grid[int.Max(r - 1, 0), a];
                var normal = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(along, up));
                var outward = grid[r, a] with { Y = 0 };
                // Out from the axis, or up where the ring closes over the top
                var facing = outward.LengthSquared() > 1e-12f ? System.Numerics.Vector3.Dot(normal, outward) : normal.Y;
                vertices.Add(new Shaders.Shared.LitVertex(grid[r, a], facing < 0 ? -normal : normal));
            }
        }

        for (var r = 0; r < rows - 1; r++)
        {
            for (var a = 0; a < around; a++)
            {
                var at = (uint)(r * (around + 1) + a);
                var next = at + (uint)(around + 1);
                indices.AddRange([at, at + 1, next, next, at + 1, next + 1]);
            }
        }

        return new MeshBuffers<Shaders.Shared.LitVertex> { Vertices = [.. vertices], Indices = [.. indices] };
    }
}
