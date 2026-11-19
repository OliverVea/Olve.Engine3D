namespace Olve.Engine3D.Camera;

public interface ICamera
{
    Matrix4x4 GetViewMatrix();
    Matrix4x4 GetProjectionMatrix();
}