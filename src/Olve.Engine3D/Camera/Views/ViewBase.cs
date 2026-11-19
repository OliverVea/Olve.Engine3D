namespace Olve.Engine3D.Camera.Views;

public abstract class ViewBase : IView
{
    public Vector3 Position { get; set; }
    public Matrix4x4 Rotation { get; set; } = Matrix4x4.Identity;

    public abstract Matrix4x4 GetViewMatrix();
}