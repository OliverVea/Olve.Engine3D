namespace Olve.Engine3D.Camera.Projections;

public class OrthographicProjection : ProjectionBase
{
    public float OrthographicSize { get; set; } = 10f;

    public override Matrix4x4 GetProjectionMatrix()
    {
        var halfWidth = OrthographicSize * AspectRatio;
        var halfHeight = OrthographicSize;

        return Matrix4x4.CreateOrthographic(halfWidth * 2, halfHeight * 2, NearPlane, FarPlane);
    }
}