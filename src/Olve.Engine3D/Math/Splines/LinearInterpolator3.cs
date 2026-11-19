namespace Olve.Engine3D.Math.Splines;

public class LinearInterpolator3(IReadOnlyList<KeyFrame<Vector3>> keyFrames) : LinearInterpolator<Vector3>(keyFrames)
{
    public override Vector3 Min { get; set; } = Vector3.One * float.MinValue;
    public override Vector3 Max { get; set; } = Vector3.One * float.MaxValue;

    protected override Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        return a + (b - a) * t;
    }

    protected override Vector3 Clamp(Vector3 value, Vector3 min, Vector3 max)
    {
        return Vector3.Clamp(value, min, max);
    }
}