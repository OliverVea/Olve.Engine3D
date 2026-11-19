using Olve.Engine3D.Utilities;
using ControllerCamera = Olve.Engine3D.Camera.Camera<Olve.Engine3D.Camera.Views.FirstPersonView, Olve.Engine3D.Camera.Projections.PerspectiveProjection>;

namespace Olve.Engine3D.Camera.Controllers;

public class PerspectiveCameraController(ControllerCamera camera) : CameraControllerBase<ControllerCamera>(camera)
{
    private readonly ControllerCamera _camera = camera;
    public float LinearSpeed { get; set; } = 10.0f;
    public float AngularSpeed { get; set; } = PiOver2;
    public float ZoomSpeed { get; set; } = 5f;

    private Vector3 Forward => Vector3.Transform(Vector3.UnitZ, _camera.View.Rotation);

    public override void Move(Vector3 direction, TimeSpan deltaTime, float scale = 1f)
    {
        if (direction.LengthSquared() < 0.1f)
        {
            return;
        }

        direction = Vector3.Normalize(direction) * scale;

        var rotatedDirection = Vector3.Transform(direction, _camera.View.Rotation);
        _camera.View.Position += rotatedDirection * LinearSpeed * deltaTime.InSeconds();
    }

    public override void Rotate(Vector2 rotation, TimeSpan deltaTime, float scale = 1f)
    {
        if (rotation.LengthSquared() < 0.0000001f)
        {
            return;
        }

        rotation = Vector2.Normalize(rotation) * scale;

        var yaw = -rotation.X * AngularSpeed * deltaTime.InSeconds();
        var pitch = -rotation.Y * AngularSpeed * deltaTime.InSeconds();

        var right = Vector3.Transform(Vector3.UnitX, _camera.View.Rotation);
        var yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
        var pitchRotation = Quaternion.CreateFromAxisAngle(right, pitch);

        var yawRotationMatrix = Matrix4x4.CreateFromQuaternion(yawRotation);
        var pitchRotationMatrix = Matrix4x4.CreateFromQuaternion(pitchRotation);

        _camera.View.Rotation = pitchRotationMatrix * yawRotationMatrix * _camera.View.Rotation;
    }

    public override void Zoom(float delta, TimeSpan deltaTime, float scale = 1f)
    {
        if (MathF.Abs(delta) < 0.1f)
        {
            return;
        }

        delta = float.Clamp(delta, -1f, 1f);

        var zoomAmount = float.Pow(ZoomSpeed, delta) * scale * deltaTime.InSeconds();

        _camera.Projection.FieldOfView = float.Clamp(
            _camera.Projection.FieldOfView - zoomAmount,
            MathF.PI / 6,
            MathF.PI / 2
        );
    }
}