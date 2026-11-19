using System.Diagnostics.CodeAnalysis;

namespace Olve.Engine3D.Math.Splines;

public class CatmullRom4 : CatmullRom<Vector4>
{
    private readonly Vector4[] _coefficients;

    [SetsRequiredMembers]
    public CatmullRom4(IReadOnlyList<KeyFrame<Vector4>> keyFrames, float? min = null, float? max = null) : base(keyFrames)
    {
        Min = Vector4.One * (min ?? float.MinValue);
        Max = Vector4.One * (max ?? float.MaxValue);

        _coefficients = new Vector4[(KeyFrames.Length - 1) * SplineCoefficients.PerSegment];

        for (var i = 1; i < KeyFrames.Length - 2; i++)
        {
            var p0 = KeyFrames[i - 1].Value;
            var p1 = KeyFrames[i].Value;
            var p2 = KeyFrames[i + 1].Value;
            var p3 = KeyFrames[i + 2].Value;

            SplineCoefficients.Compute(Characteristics.CatmullRom, p0, p1, p2, p3, Segment(i));
        }
    }

    protected override Vector4 Sample(int segmentIndex, in Vector4 t)
    {
        var result = SplineCoefficients.Evaluate(t, Segment(segmentIndex));

        return Vector4.Clamp(result, Min, Max);
    }

    private Span<Vector4> Segment(int index) =>
        _coefficients.AsSpan(index * SplineCoefficients.PerSegment, SplineCoefficients.PerSegment);
}
