namespace Olve.Engine3D.Camera.Projections;

public class PerspectiveProjection : ProjectionBase
{
    public float FieldOfView { get; set; } = PiOver4;

    public override Matrix4x4 GetProjectionMatrix()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, AspectRatio, NearPlane, FarPlane);
    }
}