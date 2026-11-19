namespace Olve.Engine3D.Math.Splines;

public class Hermite1 : Hermite<float>
{
    private readonly Vector4[] _coefficients;

    public Hermite1(IReadOnlyList<KeyFrame<Knot>> keyFrames) : base(keyFrames)
    {
        _coefficients = new Vector4[KeyFrames.Count - 1];

        for (var i = 0; i < KeyFrames.Count - 1; i++)
        {
            var p0 = KeyFrames[i].Value.Value;
            var p1 = KeyFrames[i + 1].Value.Value;
            var m0 = KeyFrames[i].Value.TangentOut;
            var m1 = KeyFrames[i + 1].Value.TangentIn;

            var geometry = new Vector4(p0, p1, m0, m1);
            var characteristic = Characteristics.Hermite;

            _coefficients[i] = new Vector4(
                Vector4.Dot(characteristic.X, geometry),
                Vector4.Dot(characteristic.Y, geometry),
                Vector4.Dot(characteristic.Z, geometry),
                Vector4.Dot(characteristic.W, geometry));
        }
    }

    protected override float SampleDerivative(int segmentIndex, in Vector4 tPrime, float segmentDuration)
    {
        var dLocal = Vector4.Dot(tPrime, _coefficients[segmentIndex]);
        return dLocal / segmentDuration;
    }

    protected override float Sample(int segmentIndex, in Vector4 t)
    {
        var result = Vector4.Dot(t, _coefficients[segmentIndex]);
        return result;
    }
}