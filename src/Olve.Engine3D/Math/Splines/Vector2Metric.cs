namespace Olve.Engine3D.Math.Splines;

public sealed class Vector2Metric : IArcLengthMetric<Vector2>
{
    public float Distance(in Vector2 a, in Vector2 b)
        => MathF.Sqrt((a.X - b.X)*(a.X - b.X) + (a.Y - b.Y)*(a.Y - b.Y));
}