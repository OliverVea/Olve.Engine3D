namespace Olve.Engine3D.Math.Splines;

public class Hermite3 : Hermite<Vector3>
{
    private readonly Vector3[] _coefficients;

    public Hermite3(IReadOnlyList<KeyFrame<Knot>> keyFrames) : base(keyFrames)
    {
        _coefficients = new Vector3[(KeyFrames.Count - 1) * SplineCoefficients.PerSegment];

        for (var i = 0; i < KeyFrames.Count - 1; i++)
        {
            var p0 = KeyFrames[i].Value.Value;
            var p1 = KeyFrames[i + 1].Value.Value;
            var m0 = KeyFrames[i].Value.TangentOut;
            var m1 = KeyFrames[i + 1].Value.TangentIn;

            SplineCoefficients.Compute(Characteristics.Hermite, p0, p1, m0, m1, Segment(i));
        }
    }

    protected override Vector3 SampleDerivative(int segmentIndex, in Vector4 tPrime, float segmentDuration)
    {
        var dLocal = SplineCoefficients.Evaluate(tPrime, Segment(segmentIndex));
        return dLocal / segmentDuration;
    }

    protected override Vector3 Sample(int segmentIndex, in Vector4 t) =>
        SplineCoefficients.Evaluate(t, Segment(segmentIndex));

    private Span<Vector3> Segment(int index) =>
        _coefficients.AsSpan(index * SplineCoefficients.PerSegment, SplineCoefficients.PerSegment);
}
