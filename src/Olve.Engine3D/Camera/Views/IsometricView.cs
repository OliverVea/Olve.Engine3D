namespace Olve.Engine3D.Camera.Views;

    public class IsometricView : ViewBase
    {
        public override Matrix4x4 GetViewMatrix()
        {
            var translation = Matrix4x4.CreateTranslation(-Position);
            var transform = translation * Rotation;


            return transform;
        }
    }