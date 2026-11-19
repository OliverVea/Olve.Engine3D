using System.Numerics;

namespace Olve.Engine3D.Tests.Geometry;

/// <summary>A triangle mesh as primitives emit it, and the properties the primitive tests check on it.</summary>
internal sealed record Mesh(Vector3[] Positions, Vector3[] Normals, uint[] Indices)
{
    private const float WeldGrid = 1e-4f;

    public IEnumerable<(int A, int B, int C)> Triangles =>
        Enumerable.Range(0, Indices.Length / 3).Select(t => ((int)Indices[3 * t], (int)Indices[3 * t + 1], (int)Indices[3 * t + 2]));

    /// <summary>Triangles with an area; pole fans and squeezed bevels leave some with none.</summary>
    public IEnumerable<(int A, int B, int C)> SolidTriangles => Triangles.Where(t => FaceCross(t).Length() > 1e-9f);

    public Vector3 FaceCross((int A, int B, int C) t) =>
        Vector3.Cross(Positions[t.B] - Positions[t.A], Positions[t.C] - Positions[t.A]);

    /// <summary>
    /// Watertight with consistent winding: after welding vertices at the same position, every edge of a solid triangle
    /// is used once in each direction.
    /// </summary>
    public bool IsClosed()
    {
        var welded = Positions.Select(Weld).ToArray();
        var directed = new Dictionary<((int, int, int), (int, int, int)), int>();
        foreach (var (a, b, c) in SolidTriangles)
        {
            foreach (var (from, to) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = (welded[from], welded[to]);
                directed[key] = directed.GetValueOrDefault(key) + 1;
            }
        }

        return directed.Count > 0 && directed.All(e => e.Value == 1 && directed.GetValueOrDefault((e.Key.Item2, e.Key.Item1)) == 1);
    }

    /// <summary>The enclosed volume (divergence theorem); positive when the triangles face outward.</summary>
    public float Volume() =>
        Triangles.Sum(t => Vector3.Dot(Positions[t.A], Vector3.Cross(Positions[t.B], Positions[t.C]))) / 6;

    public bool NormalsAreUnitLength() => Normals.All(n => MathF.Abs(n.Length() - 1) < 1e-4f);

    /// <summary>Every solid triangle winds counter-clockwise seen from the side its vertex normals point to.</summary>
    public bool WindingAgreesWithNormals() =>
        SolidTriangles.All(t => new[] { t.A, t.B, t.C }.All(v => Vector3.Dot(FaceCross(t), Normals[v]) > 0));

    /// <summary>For a convex shape round <paramref name="centre"/>: every normal points away from it.</summary>
    public bool NormalsPointAwayFrom(Vector3 centre) =>
        Positions.Zip(Normals).All(pn => Vector3.Dot(pn.Second, pn.First - centre) > 0);

    public bool IsFinite() => Positions.Concat(Normals).All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z));

    private static (int, int, int) Weld(Vector3 p) =>
        ((int)MathF.Round(p.X / WeldGrid), (int)MathF.Round(p.Y / WeldGrid), (int)MathF.Round(p.Z / WeldGrid));
}
