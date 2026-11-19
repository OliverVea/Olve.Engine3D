using System.Numerics;
using Olve.Engine3D.Camera;
using Olve.Engine3D.Camera.Projections;
using Olve.Engine3D.Camera.Views;

namespace Olve.Engine3D.Tests.Camera;

public class FirstPersonRayTests
{
    public static IEnumerable<Func<(string Name, Matrix4x4 Rotation, float Aspect)>> Cameras() =>
    [
        () => ("looking ahead", Matrix4x4.Identity, 1f),
        () => ("turned and tilted down, wide", Matrix4x4.CreateRotationX(0.4f) * Matrix4x4.CreateRotationY(-0.7f), 16f / 9),
        () => ("rolled, tall", Matrix4x4.CreateRotationZ(0.3f) * Matrix4x4.CreateRotationY(2.5f), 9f / 16),
    ];

    [Test]
    [MethodDataSource(nameof(Cameras))]
    public async Task The_ray_through_a_point_on_screen_passes_through_what_is_drawn_there(string name, Matrix4x4 rotation, float aspect)
    {
        var camera = Olve.Engine3D.Camera.Camera.Create(
            new FirstPersonView { Position = new Vector3(1, 1.2f, -2), Rotation = rotation },
            new PerspectiveProjection { AspectRatio = aspect, FieldOfView = 1.1f });
        var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix();
        var forward = Vector3.Transform(Vector3.UnitZ, rotation);
        var side = Vector3.Transform(Vector3.UnitX, rotation);
        var up = Vector3.Transform(Vector3.UnitY, rotation);

        Vector3[] seen =
        [
            camera.View.Position + forward * 3,
            camera.View.Position + forward * 5 + side * 1.5f + up * 0.8f,
            camera.View.Position + forward * 1 - side * 0.3f - up * 0.4f,
        ];

        foreach (var world in seen)
        {
            var clip = Vector4.Transform(new Vector4(world, 1), viewProjection);
            var ndc = new Vector2(clip.X, clip.Y) / clip.W;

            var ray = await camera.GetRay(ndc).AssertSuccessAndGetAsync();

            var along = Vector3.Dot(world - ray.Origin, ray.Direction);
            await Assert.That(ray.Origin).IsEqualTo(camera.View.Position);
            await Assert.That(MathF.Abs(ray.Direction.Length() - 1)).IsLessThan(1e-5f);
            await Assert.That(along).IsGreaterThan(0);
            await Assert.That(Vector3.Distance(ray.Origin + ray.Direction * along, world)).IsLessThan(1e-3f);
        }
    }

    [Test]
    public async Task The_middle_of_the_screen_looks_where_the_camera_faces()
    {
        var rotation = Matrix4x4.CreateRotationY(0.6f);
        var camera = Olve.Engine3D.Camera.Camera.Create(new FirstPersonView { Rotation = rotation }, new PerspectiveProjection());

        var ray = await camera.GetRay(Vector2.Zero).AssertSuccessAndGetAsync();

        await Assert.That(Vector3.Distance(ray.Direction, Vector3.Transform(Vector3.UnitZ, rotation))).IsLessThan(1e-5f);
    }
}
