using System.Diagnostics.CodeAnalysis;

namespace Olve.Engine3D.Math.Splines;

public class CatmullRom3 : CatmullRom<Vector3>
{
    private readonly Vector3[] _coefficients;

    [SetsRequiredMembers]
    public CatmullRom3(IReadOnlyList<KeyFrame<Vector3>> keyFrames, float? min = null, float? max = null) : base(keyFrames)
    {
        Min = Vector3.One * (min ?? float.MinValue);
        Max = Vector3.One * (max ?? float.MaxValue);

        _coefficients = new Vector3[(KeyFrames.Length - 3) * SplineCoefficients.PerSegment];

        for (var i = 0; i < KeyFrames.Length - 3; i++)
        {
            var p0 = KeyFrames[i].Value;
            var p1 = KeyFrames[i + 1].Value;
            var p2 = KeyFrames[i + 2].Value;
            var p3 = KeyFrames[i + 3].Value;

            SplineCoefficients.Compute(Characteristics.CatmullRom, p0, p1, p2, p3, Segment(i));
        }
    }

    protected override Vector3 Sample(int segmentIndex, in Vector4 t)
    {
        var result = SplineCoefficients.Evaluate(t, Segment(segmentIndex));

        return Vector3.Clamp(result, Min, Max);
    }

    private Span<Vector3> Segment(int index) =>
        _coefficients.AsSpan(index * SplineCoefficients.PerSegment, SplineCoefficients.PerSegment);
}
