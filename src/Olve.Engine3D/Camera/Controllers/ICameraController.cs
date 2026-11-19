namespace Olve.Engine3D.Camera.Controllers;

public interface ICameraController
{
    ICamera Camera { get; }
    
    void Move(Vector3 direction, TimeSpan deltaTime, float scale = 1f);
    void Rotate(Vector2 rotation, TimeSpan deltaTime, float scale = 1f);
    void Zoom(float delta, TimeSpan deltaTime, float scale = 1f);
}