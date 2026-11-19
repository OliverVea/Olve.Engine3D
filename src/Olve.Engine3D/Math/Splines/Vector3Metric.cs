namespace Olve.Engine3D.Math.Splines;

public sealed class Vector3Metric : IArcLengthMetric<Vector3>
{
    public float Distance(in Vector3 a, in Vector3 b)
        => MathF.Sqrt((a.X - b.X)*(a.X - b.X) + (a.Y - b.Y)*(a.Y - b.Y) + (a.Z - b.Z)*(a.Z - b.Z));
}