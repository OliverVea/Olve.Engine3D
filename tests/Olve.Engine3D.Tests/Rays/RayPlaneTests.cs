using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Rays;

public class RayPlaneTests
{
    public static IEnumerable<Func<(string Name, Ray Ray, Plane Plane, Vector3? Hit)>> Crossings() =>
    [
        () => ("straight down onto the ground", new Ray(new(1, 5, 2), -Vector3.UnitY), new Plane(Vector3.UnitY, 0), new Vector3(1, 0, 2)),
        () => ("through the back of a plane", new Ray(new(0, 0, -3), Vector3.UnitZ), new Plane(Vector3.UnitZ, -1), new Vector3(0, 0, 1)),
        () => ("a slanted panel, long direction", new Ray(Vector3.Zero, new(2, 2, 0)), Plane.CreateFromVertices(new(1, 0, 0), new(1, 0, 1), new(0, 1, 0)), new Vector3(0.5f, 0.5f, 0)),
        () => ("unnormalized plane", new Ray(Vector3.Zero, Vector3.UnitX), new Plane(new Vector3(4, 0, 0), -12), new Vector3(3, 0, 0)),
        () => ("from on the plane", new Ray(new(2, 0, 2), Vector3.UnitY), new Plane(Vector3.UnitY, 0), new Vector3(2, 0, 2)),
        () => ("parallel above", new Ray(new(0, 1, 0), Vector3.UnitX), new Plane(Vector3.UnitY, 0), null),
        () => ("parallel within", new Ray(Vector3.Zero, Vector3.UnitX), new Plane(Vector3.UnitY, 0), null),
        () => ("behind", new Ray(new(0, 1, 0), Vector3.UnitY), new Plane(Vector3.UnitY, 0), null),
    ];

    [Test]
    [MethodDataSource(nameof(Crossings))]
    public async Task A_ray_hits_a_plane_ahead_of_it_where_they_cross(string name, Ray ray, Plane plane, Vector3? hit)
    {
        var hits = ray.TryIntersectPlane(plane, out var point);

        await Assert.That(hits).IsEqualTo(hit.HasValue);
        if (hit is { } expected)
        {
            await Assert.That(Vector3.Distance(point, expected)).IsLessThan(1e-5f);
            await Assert.That(ray.TryGetDistanceToPlane(plane, out var t)).IsTrue();
            await Assert.That(Vector3.Distance(ray.Origin + t * ray.Direction, expected)).IsLessThan(1e-5f);
        }
    }
}
