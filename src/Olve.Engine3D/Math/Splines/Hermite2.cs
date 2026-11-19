namespace Olve.Engine3D.Math.Splines;

public class Hermite2 : Hermite<Vector2>
{
    private readonly Vector2[] _coefficients;

    public Hermite2(IReadOnlyList<KeyFrame<Knot>> keyFrames) : base(keyFrames)
    {
        _coefficients = new Vector2[(KeyFrames.Count - 1) * SplineCoefficients.PerSegment];

        for (var i = 0; i < KeyFrames.Count - 1; i++)
        {
            var p0 = KeyFrames[i].Value.Value;
            var p1 = KeyFrames[i + 1].Value.Value;
            var m0 = KeyFrames[i].Value.TangentOut;
            var m1 = KeyFrames[i + 1].Value.TangentIn;

            SplineCoefficients.Compute(Characteristics.Hermite, p0, p1, m0, m1, Segment(i));
        }
    }

    protected override Vector2 SampleDerivative(int segmentIndex, in Vector4 tPrime, float segmentDuration)
    {
        var dLocal = SplineCoefficients.Evaluate(tPrime, Segment(segmentIndex));
        return dLocal / segmentDuration;
    }

    protected override Vector2 Sample(int segmentIndex, in Vector4 t) =>
        SplineCoefficients.Evaluate(t, Segment(segmentIndex));

    private Span<Vector2> Segment(int index) =>
        _coefficients.AsSpan(index * SplineCoefficients.PerSegment, SplineCoefficients.PerSegment);
}
