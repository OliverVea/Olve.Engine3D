using System.Numerics;
using Olve.Engine3D.Camera.Controllers;

namespace Olve.Engine3D.Tests.Camera;

public class IsometricOrthographicCameraControllerTests
{
    private static IsometricOrthographicCameraController CreateController() =>
        IsometricOrthographicCameraController.Create(new Vector2(16, 9), new Vector3(10, 0, 17), new Vector3(0.701f, -1, 0.701f));

    [Test]
    public async Task Rotating_Keeps_Looking_At_The_Same_Ground_Point()
    {
        var controller = CreateController();
        var before = controller.GetGroundTarget();

        controller.Rotate(new Vector2(1, 0), TimeSpan.FromSeconds(0.5));

        var after = controller.GetGroundTarget();
        await Assert.That(Vector3.Distance(before, after)).IsLessThan(1e-3f);
        await Assert.That(Vector3.Distance(before, new Vector3(10, 0, 17))).IsLessThan(1e-3f);
    }

    [Test]
    public async Task A_Quarter_Turn_Turns_The_View_A_Quarter_About_The_Vertical()
    {
        var controller = CreateController();
        var forward = controller.Forward;

        // A second at full input turns a quarter
        controller.Rotate(new Vector2(1, 0), TimeSpan.FromSeconds(1));

        var turned = controller.Forward;
        await Assert.That(MathF.Abs(turned.Y - forward.Y)).IsLessThan(1e-4f);
        await Assert.That(MathF.Abs(Vector2.Dot(new Vector2(forward.X, forward.Z), new Vector2(turned.X, turned.Z)))).IsLessThan(1e-4f);
    }
}
