namespace Olve.Engine3D.Camera.Views;

public class FirstPersonView: ViewBase
{
    public override Matrix4x4 GetViewMatrix()
    {
        var forward = Vector3.Transform(Vector3.UnitZ, Rotation);
        var up = Vector3.Transform(Vector3.UnitY, Rotation);

        return Matrix4x4.CreateLookAt(Position, Position + forward, up);
    }
}