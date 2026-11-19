namespace Olve.Engine3D.Camera.Views;

public interface IView
{
    Vector3 Position { get; set; }
    Matrix4x4 Rotation { get; set; }

    Matrix4x4 GetViewMatrix();
}