using Olve.Engine3D.Camera.Projections;
using Olve.Engine3D.Camera.Views;
using Olve.Engine3D.Utilities;
using ControllerCamera = Olve.Engine3D.Camera.Camera<Olve.Engine3D.Camera.Views.IsometricView, Olve.Engine3D.Camera.Projections.OrthographicProjection>;

namespace Olve.Engine3D.Camera.Controllers;

public class IsometricOrthographicCameraController(ControllerCamera camera) : CameraControllerBase<ControllerCamera>(camera)
{
    private readonly ControllerCamera _camera = camera;
    public float MoveSpeed { get; set; } = 2.0f;
    public float ZoomSpeed { get; set; } = 50f;

    public override void Move(Vector3 direction, TimeSpan deltaTime, float scale = 1f)
    {
        if (direction.LengthSquared() < 0.1f)
        {
            return;
        }

        var cameraToWorld = Matrix4x4.Transpose(_camera.View.Rotation);
        var worldDirection = Vector3.Transform(direction, cameraToWorld);

        worldDirection.Y = 0;
        worldDirection = Vector3.Normalize(-worldDirection);

        var movement = worldDirection * MoveSpeed * deltaTime.InSeconds() * _camera.Projection.OrthographicSize;

        _camera.View.Position += movement;
    }


    /// <summary>Radians per second at full input.</summary>
    public float RotateSpeed { get; set; } = MathF.PI / 2f;

    /// <summary>Turns the camera about the vertical axis through the ground point it looks at; <c>rotation.X</c> is the turn.</summary>
    public override void Rotate(Vector2 rotation, TimeSpan deltaTime, float scale = 1f)
    {
        if (MathF.Abs(rotation.X) < 0.1f)
        {
            return;
        }

        TurnBy(float.Clamp(rotation.X, -1f, 1f) * RotateSpeed * scale * deltaTime.InSeconds());
    }

    /// <summary>Turns the camera by <paramref name="radians"/> about the vertical axis through the ground point it looks at.</summary>
    public void TurnBy(float radians)
    {
        var turn = Matrix4x4.CreateRotationY(radians);
        Matrix4x4.Invert(turn, out var inverseTurn);

        var pivot = GetGroundTarget();
        _camera.View.Position = pivot + Vector3.Transform(_camera.View.Position - pivot, turn);
        _camera.View.Rotation = inverseTurn * _camera.View.Rotation;
    }

    /// <summary>The camera's forward axis in world space.</summary>
    public Vector3 Forward => new(_camera.View.Rotation.M13, _camera.View.Rotation.M23, _camera.View.Rotation.M33);

    /// <summary>Where the line through the camera along its forward axis meets the ground (y = 0).</summary>
    public Vector3 GetGroundTarget()
    {
        var forward = Forward;
        var position = _camera.View.Position;
        return position - forward * (position.Y / forward.Y);
    }

    public override void Zoom(float delta, TimeSpan deltaTime, float scale = 1f)
    {
        if (MathF.Abs(delta) < 0.1f)
        {
            return;
        }

        delta = float.Clamp(delta, -1f, 1f);

        var zoomAmount = ZoomSpeed * delta * scale * deltaTime.InSeconds();
        _camera.Projection.OrthographicSize = float.Max(1f, _camera.Projection.OrthographicSize - zoomAmount);
    }

    public static IsometricOrthographicCameraController Create(
        Vector2 windowSize,
        Vector3 target,
        Vector3 viewDirection,
        float orthographicSize = 10f)
    {
        var cameraPosition = target + Vector3.Normalize(viewDirection);

        var forward = Vector3.Normalize(target - cameraPosition);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        var up = Vector3.Cross(forward, right);

        var worldRotation = new Matrix4x4(
            right.X,   up.X,   forward.X,  0,
            right.Y,   up.Y,   forward.Y,  0,
            right.Z,   up.Z,   forward.Z,  0,
            0,         0,      0,          1);

        IsometricView view = new() { Position = cameraPosition, Rotation = worldRotation };

        var aspectRatio = windowSize.X / windowSize.Y;
        // The camera sits next to what it looks at, so it also sees what is behind it: the projection's depth is [0, 1]
        // from the near plane, which WebGPU clips to
        const float nearPlane = -10000f;
        const float farPlane = 10000f;

        OrthographicProjection projection = new() { AspectRatio = aspectRatio, NearPlane = nearPlane, FarPlane = farPlane, OrthographicSize = orthographicSize};

        var camera = Olve.Engine3D.Camera.Camera.Create(view, projection);

        return new IsometricOrthographicCameraController(camera);
    }
}