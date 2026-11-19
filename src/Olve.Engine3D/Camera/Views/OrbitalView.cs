namespace Olve.Engine3D.Camera.Views;

public class OrbitalView : IView
{
    public Vector3 Target { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }

    public Vector3 Position
    {
        get => CalculatePosition();
        set => SetPosition(value);
    }

    public Matrix4x4 Rotation
    {
        get => CalculateRotation();
        set => SetRotation(value);
    }

    private Vector3 CalculatePosition()
    {
        throw new NotImplementedException();
    }

    private void SetPosition(Vector3 value)
    {
        throw new NotImplementedException();
    }

    private Matrix4x4 CalculateRotation()
    {
        throw new NotImplementedException();
    }

    private void SetRotation(Matrix4x4 value)
    {
        throw new NotImplementedException();
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Target, new Vector3(0, 1, 0));
    }
}