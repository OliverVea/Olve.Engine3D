using System.Diagnostics.CodeAnalysis;

namespace Olve.Engine3D.Math.Splines;

public class CatmullRom2 : CatmullRom<Vector2>
{
    private readonly Vector2[] _coefficients;

    [SetsRequiredMembers]
    public CatmullRom2(IReadOnlyList<KeyFrame<Vector2>> keyFrames, float? min = null, float? max = null) : base(keyFrames)
    {
        Min = Vector2.One * (min ?? float.MinValue);
        Max = Vector2.One * (max ?? float.MaxValue);

        _coefficients = new Vector2[(KeyFrames.Length - 1) * SplineCoefficients.PerSegment];

        for (var i = 1; i < KeyFrames.Length - 2; i++)
        {
            var p0 = KeyFrames[i - 1].Value;
            var p1 = KeyFrames[i].Value;
            var p2 = KeyFrames[i + 1].Value;
            var p3 = KeyFrames[i + 2].Value;

            SplineCoefficients.Compute(Characteristics.CatmullRom, p0, p1, p2, p3, Segment(i));
        }
    }

    protected override Vector2 Sample(int segmentIndex, in Vector4 t)
    {
        var result = SplineCoefficients.Evaluate(t, Segment(segmentIndex));
        return Vector2.Clamp(result, Min, Max);
    }

    private Span<Vector2> Segment(int index) =>
        _coefficients.AsSpan(index * SplineCoefficients.PerSegment, SplineCoefficients.PerSegment);
}
