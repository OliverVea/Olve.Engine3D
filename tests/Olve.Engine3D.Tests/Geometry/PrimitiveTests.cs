using System.Numerics;
using Olve.Engine3D.Geometry.Primitives;
using Olve.Results;

namespace Olve.Engine3D.Tests.Geometry;

public class PrimitiveTests
{
    private static Mesh Build(LatheShape shape)
    {
        var mesh = new Mesh(new Vector3[shape.VertexCount], new Vector3[shape.VertexCount], new uint[shape.IndexCount]);
        Lathe.GetVertices(shape, mesh.Positions, mesh.Normals);
        Lathe.GetIndices(shape, mesh.Indices);
        return mesh;
    }

    private static Mesh Build(BevelledBoxShape shape)
    {
        var mesh = new Mesh(new Vector3[shape.VertexCount], new Vector3[shape.VertexCount], new uint[shape.IndexCount]);
        BevelledBox.GetVertices(shape, mesh.Positions, mesh.Normals);
        BevelledBox.GetIndices(shape, mesh.Indices);
        return mesh;
    }

    private static Mesh Build(TubeShape shape, Vector3[] path)
    {
        var mesh = new Mesh(new Vector3[shape.VertexCount], new Vector3[shape.VertexCount], new uint[shape.IndexCount]);
        Tube.GetVertices(shape, path, mesh.Positions, mesh.Normals);
        Tube.GetIndices(shape, mesh.Indices);
        return mesh;
    }

    private static T Valid<T>(Result<T> result) =>
        result.TryPickProblems(out var problems, out var value)
            ? throw new InvalidOperationException(string.Join(": ", problems.Select(p => p.ToBriefString())))
            : value;

    private static string Problem<T>(Result<T> result) =>
        result.TryPickProblems(out var problems) ? string.Join(": ", problems.Select(p => p.ToBriefString())) : "succeeded";

    /// <summary>A polygon of <paramref name="segments"/> sides inscribed in a circle of radius <paramref name="r"/>.</summary>
    private static float PolygonArea(int segments, float r) => segments / 2f * r * r * MathF.Sin(float.Tau / segments);

    /// <summary>A unit half circle from the bottom pole to the top one, on the axis exactly at both.</summary>
    private static Vector2[] HalfCircle(int steps) =>
        Enumerable.Range(0, steps + 1)
            .Select(k => k == 0 || k == steps ? new Vector2(0, k == 0 ? -1 : 1) : new Vector2(MathF.Sin(float.Pi * k / steps), -MathF.Cos(float.Pi * k / steps)))
            .ToArray();

    // Lathe

    public static IEnumerable<Func<(string Name, Vector2[] Profile, LatheCaps Caps, Vector3 Inside)>> ClosedLathes() =>
    [
        () => ("capped cylinder", [new(1, 0), new(1, 2)], LatheCaps.Both, new(0, 1, 0)),
        () => ("cone on a disc", [new(1, 0), new(0, 1)], LatheCaps.Start, new(0, 0.3f, 0)),
        () => ("knob with a chamfer", [new(0.5f, 0), new(0.5f, 0.6f), new(0.4f, 0.7f), new(0, 0.7f)], LatheCaps.Start, new(0, 0.3f, 0)),
        () => ("sphere", HalfCircle(16), LatheCaps.None, Vector3.Zero),
    ];

    [Test]
    [MethodDataSource(nameof(ClosedLathes))]
    public async Task A_closed_lathe_is_watertight_with_unit_normals_facing_out(string name, Vector2[] profile, LatheCaps caps, Vector3 inside)
    {
        var mesh = Build(Valid(LatheShape.Create(profile, 24, caps)));

        await Assert.That(mesh.IsClosed()).IsTrue();
        await Assert.That(mesh.NormalsAreUnitLength()).IsTrue();
        await Assert.That(mesh.WindingAgreesWithNormals()).IsTrue();
        await Assert.That(mesh.NormalsPointAwayFrom(inside)).IsTrue();
        await Assert.That(mesh.Volume()).IsGreaterThan(0);
    }

    [Test]
    public async Task A_capped_cylinder_has_square_edges_and_the_volume_of_its_prism()
    {
        var shape = Valid(LatheShape.Create([new(1, 0), new(1, 2)], 12, LatheCaps.Both));
        var mesh = Build(shape);

        // Axis, rim and rim again on each end: the rims split between the cap and the side
        await Assert.That((shape.VertexCount, shape.IndexCount)).IsEqualTo((6 * 12, 3 * 12 * 6));
        await Assert.That(mesh.Volume()).IsEqualTo(PolygonArea(12, 1) * 2).Within(1e-4f);

        var side = mesh.Positions.Zip(mesh.Normals).Where(pn => pn.Second.Y == 0).ToList();
        await Assert.That(side.Count).IsEqualTo(2 * 12);
        await Assert.That(side.All(pn => Vector3.Distance(pn.Second, Vector3.Normalize(pn.First with { Y = 0 })) < 1e-5f)).IsTrue();
        await Assert.That(mesh.Normals.Count(n => n == -Vector3.UnitY)).IsEqualTo(2 * 12);
        await Assert.That(mesh.Normals.Count(n => n == Vector3.UnitY)).IsEqualTo(2 * 12);
    }

    [Test]
    public async Task A_smooth_profile_gets_smooth_normals_and_a_sharp_one_splits_them()
    {
        // A sphere's normals point straight out from its centre; poles face along the axis
        var sphere = Build(Valid(LatheShape.Create(HalfCircle(16), 24)));
        await Assert.That(sphere.Positions.Length).IsEqualTo(17 * 24);
        await Assert.That(sphere.Positions.Zip(sphere.Normals).All(pn => Vector3.Distance(pn.Second, Vector3.Normalize(pn.First)) < 1e-4f)).IsTrue();

        // The same quarter-turn corner is smooth when the crease angle allows it
        Vector2[] corner = [new(1, 0), new(1, 1), new(0, 1)];
        await Assert.That(Valid(LatheShape.Create(corner, 8, creaseAngle: float.Pi / 6)).RingCount).IsEqualTo(4);
        await Assert.That(Valid(LatheShape.Create(corner, 8, creaseAngle: float.Pi / 2)).RingCount).IsEqualTo(3);
    }

    public static IEnumerable<Func<(Vector2[] Profile, int Segments, float CreaseAngle, string Problem)>> InvalidLathes() =>
    [
        () => ([new(1, 0)], 8, 0.5f, "A lathe profile needs at least 2 points, got 1"),
        () => ([new(1, 0), new(1, 1)], 2, 0.5f, "A lathe needs at least 3 segments, got 2"),
        () => ([new(1, 0), new(-1, 1)], 8, 0.5f, "Lathe profile point 1 must be finite with a radius of at least 0, got <-1, 1>"),
        () => ([new(1, 0), new(1, 0), new(1, 1)], 8, 0.5f, "Lathe profile points 0 and 1 are the same point <1, 0>"),
        () => ([new(1, 0), new(1, 1)], 8, 4f, "A lathe's crease angle must be between 0 and pi, got 4"),
    ];

    [Test]
    [MethodDataSource(nameof(InvalidLathes))]
    public async Task A_lathe_that_cant_be_built_says_why(Vector2[] profile, int segments, float creaseAngle, string problem)
    {
        await Assert.That(Problem(LatheShape.Create(profile, segments, creaseAngle: creaseAngle))).IsEqualTo(problem);
    }

    // Bevelled box

    [Test]
    [Arguments(2f, 1f, 0.5f, 0.1f, 2)]
    [Arguments(1f, 1f, 1f, 0.5f, 4)]
    [Arguments(3f, 0.2f, 1f, 0.1f, 6)]
    public async Task A_bevelled_box_is_a_closed_convex_box_rounded_by_its_radius(float x, float y, float z, float radius, int segments)
    {
        var size = new Vector3(x, y, z);
        var mesh = Build(Valid(BevelledBoxShape.Create(size, radius, segments)));
        var half = size / 2;
        var inner = half - new Vector3(radius);

        await Assert.That(mesh.IsClosed()).IsTrue();
        await Assert.That(mesh.NormalsAreUnitLength()).IsTrue();
        await Assert.That(mesh.WindingAgreesWithNormals()).IsTrue();
        await Assert.That(mesh.NormalsPointAwayFrom(Vector3.Zero)).IsTrue();

        // Every point lies on the box shrunk by the radius, pushed out by the radius along its normal
        await Assert.That(mesh.Positions.Zip(mesh.Normals).All(pn =>
            Vector3.Distance(pn.First, Vector3.Clamp(pn.First, -inner, inner) + pn.Second * radius) < 1e-4f)).IsTrue();

        // It reaches its full size on every axis and no further
        var max = mesh.Positions.Aggregate(Vector3.Max);
        var min = mesh.Positions.Aggregate(Vector3.Min);
        await Assert.That(Vector3.Distance(max, half)).IsLessThan(1e-5f);
        await Assert.That(Vector3.Distance(min, -half)).IsLessThan(1e-5f);
    }

    [Test]
    public async Task A_finely_bevelled_box_holds_the_volume_of_a_rounded_box()
    {
        var size = new Vector3(2, 1, 0.5f);
        const float r = 0.2f;
        var mesh = Build(Valid(BevelledBoxShape.Create(size, r, 32)));
        var (a, b, c) = (size.X - 2 * r, size.Y - 2 * r, size.Z - 2 * r);
        var exact = a * b * c + 2 * r * (a * b + b * c + c * a) + float.Pi * r * r * (a + b + c) + 4f / 3 * float.Pi * r * r * r;

        await Assert.That(mesh.Volume()).IsEqualTo(exact).Within(exact * 1e-3f);
    }

    public static IEnumerable<Func<(Vector3 Size, float Radius, int Segments, string Problem)>> InvalidBevelledBoxes() =>
    [
        () => (new(1, 0, 1), 0.1f, 2, "A bevelled box needs a positive, finite size, got <1, 0, 1>"),
        () => (new(1, 0.4f, 1), 0.3f, 2, "A bevelled box's bevel radius must be above 0 and at most 0.2, half its smallest extent, got 0.3"),
        () => (new(1, 1, 1), 0, 2, "A bevelled box's bevel radius must be above 0 and at most 0.5, half its smallest extent, got 0"),
        () => (new(1, 1, 1), 0.1f, 3, "A bevelled box needs an even number of bevel segments, at least 2, got 3"),
        () => (new(1, 1, 1), 0.1f, 0, "A bevelled box needs an even number of bevel segments, at least 2, got 0"),
    ];

    [Test]
    [MethodDataSource(nameof(InvalidBevelledBoxes))]
    public async Task A_bevelled_box_that_cant_be_built_says_why(Vector3 size, float radius, int segments, string problem)
    {
        await Assert.That(Problem(BevelledBoxShape.Create(size, radius, segments))).IsEqualTo(problem);
    }

    // Tube

    private static Vector3[] Arc(int points) =>
        Enumerable.Range(0, points).Select(k => new Vector3(MathF.Cos(float.Pi * k / (points - 1)), MathF.Sin(float.Pi * k / (points - 1)), 0) * 2).ToArray();

    public static IEnumerable<Func<(string Name, Vector3[] Path)>> TubePaths() =>
    [
        () => ("straight", [new(0, 0, 0), new(0, 0, 1), new(0, 0, 2), new(0, 0, 3)]),
        () => ("half circle", Arc(17)),
        () => ("sag between two jacks", [new(0, 1, 0), new(0.5f, 0.4f, 0.1f), new(1, 0.2f, 0.2f), new(1.5f, 0.4f, 0.3f), new(2, 1, 0.4f)]),
    ];

    [Test]
    [MethodDataSource(nameof(TubePaths))]
    public async Task A_capped_tube_is_watertight_with_round_rings_square_to_the_path(string name, Vector3[] path)
    {
        var shape = Valid(TubeShape.Create(path.Length, 0.1f, 12));
        var mesh = Build(shape, path);

        await Assert.That(mesh.IsFinite()).IsTrue();
        await Assert.That(mesh.IsClosed()).IsTrue();
        await Assert.That(mesh.NormalsAreUnitLength()).IsTrue();
        await Assert.That(mesh.WindingAgreesWithNormals()).IsTrue();
        await Assert.That(mesh.Volume()).IsGreaterThan(0);

        // Each ring sits at the radius round its path point; its normals point straight out from it
        for (var i = 0; i < path.Length; i++)
        {
            for (var j = 0; j < 12; j++)
            {
                var v = i * 12 + j;
                await Assert.That(Vector3.Distance(mesh.Positions[v], path[i] + mesh.Normals[v] * 0.1f)).IsLessThan(1e-5f);
            }
        }
    }

    [Test]
    [Arguments("a point twice", new[] { 0f, 0, 0, 1, 0, 0, 1, 0, 0, 1, 1, 0 })]
    [Arguments("every point the same", new[] { 1f, 1, 1, 1, 1, 1, 1, 1, 1 })]
    [Arguments("turning straight back", new[] { 0f, 0, 0, 1, 0, 0, 0, 0, 0 })]
    public async Task A_tube_along_a_path_that_stops_or_turns_back_stays_finite(string name, float[] coordinates)
    {
        var path = coordinates.Chunk(3).Select(c => new Vector3(c[0], c[1], c[2])).ToArray();
        var mesh = Build(Valid(TubeShape.Create(path.Length, 0.1f, 8)), path);

        await Assert.That(mesh.IsFinite()).IsTrue();
        await Assert.That(mesh.NormalsAreUnitLength()).IsTrue();
    }

    [Test]
    public async Task A_tube_does_not_twist_along_its_path()
    {
        // Straight: every ring is the first one moved along
        Vector3[] straight = [new(0, 0, 0), new(1, 1, 1), new(2, 2, 2), new(3, 3, 3)];
        var line = Build(Valid(TubeShape.Create(4, 0.1f, 8, capped: false)), straight);
        for (var i = 1; i < 4; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                await Assert.That(Vector3.Distance(line.Positions[i * 8 + j] - straight[i], line.Positions[j] - straight[0])).IsLessThan(1e-5f);
            }
        }

        // Bending in a plane: each ring's first normal keeps its height out of that plane
        var arc = Build(Valid(TubeShape.Create(17, 0.1f, 8, capped: false)), Arc(17));
        var outOfPlane = Enumerable.Range(0, 17).Select(i => arc.Normals[i * 8].Z).ToList();
        await Assert.That(outOfPlane.Max() - outOfPlane.Min()).IsLessThan(1e-4f);
    }

    [Test]
    public async Task A_tube_rebuilt_along_a_new_path_keeps_its_indices_and_vertex_count()
    {
        var shape = Valid(TubeShape.Create(17, 0.05f, 8));
        var first = Build(shape, Arc(17));
        var moved = Build(shape, Arc(17).Select(p => p * 0.5f + Vector3.UnitZ).ToArray());

        await Assert.That((shape.VertexCount, shape.IndexCount)).IsEqualTo((17 * 8 + 2 * 9, 16 * 8 * 6 + 2 * 8 * 3));
        await Assert.That(moved.Indices).IsEquivalentTo(first.Indices);
        await Assert.That(moved.IsClosed()).IsTrue();
    }

    [Test]
    public async Task A_tube_given_a_path_of_the_wrong_length_says_so()
    {
        var shape = Valid(TubeShape.Create(4, 0.1f, 8));

        await Assert.That(() => Tube.GetVertices(shape, [Vector3.Zero, Vector3.One], new Vector3[shape.VertexCount], new Vector3[shape.VertexCount]))
            .Throws<ArgumentException>().WithMessageContaining("The tube was shaped for 4 path points, got 2");
    }

    public static IEnumerable<Func<(int Points, float Radius, int Segments, string Problem)>> InvalidTubes() =>
    [
        () => (1, 0.1f, 8, "A tube's path needs at least 2 points, got 1"),
        () => (4, 0, 8, "A tube needs a positive, finite radius, got 0"),
        () => (4, 0.1f, 2, "A tube needs at least 3 ring segments, got 2"),
    ];

    [Test]
    [MethodDataSource(nameof(InvalidTubes))]
    public async Task A_tube_that_cant_be_built_says_why(int points, float radius, int segments, string problem)
    {
        await Assert.That(Problem(TubeShape.Create(points, radius, segments))).IsEqualTo(problem);
    }
}
