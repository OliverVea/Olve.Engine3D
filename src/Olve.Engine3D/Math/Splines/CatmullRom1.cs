using System.Diagnostics.CodeAnalysis;

namespace Olve.Engine3D.Math.Splines;

public class CatmullRom1 : CatmullRom<float>
{
    private readonly Vector4[] _coefficients;

    [SetsRequiredMembers]
    public CatmullRom1(IReadOnlyList<KeyFrame<float>> keyFrames, float? min = null, float? max = null) : base(keyFrames)
    {
        Min = min ?? float.MinValue;
        Max = max ?? float.MaxValue;

        _coefficients = new Vector4[KeyFrames.Length - 3];

        for (var i = 0; i < KeyFrames.Length - 3; i++)
        {
            var p0 = KeyFrames[i].Value;
            var p1 = KeyFrames[i + 1].Value;
            var p2 = KeyFrames[i + 2].Value;
            var p3 = KeyFrames[i + 3].Value;

            var geometry = new Vector4(p0, p1, p2, p3);
            var characteristic = Characteristics.CatmullRom;

            _coefficients[i] = new Vector4(
                Vector4.Dot(characteristic.X, geometry),
                Vector4.Dot(characteristic.Y, geometry),
                Vector4.Dot(characteristic.Z, geometry),
                Vector4.Dot(characteristic.W, geometry));
        }
    }

    protected override float Sample(int segmentIndex, in Vector4 t)
    {
        var result = Vector4.Dot(t, _coefficients[segmentIndex]);
        return System.Math.Clamp(result, Min, Max);
    }
}