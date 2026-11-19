namespace Olve.Engine3D.Math;

public readonly record struct Position3D(Vector3 Position, Quaternion Rotation)
{
    public Matrix4x4 ToMatrix4x4()
    {
        var q = Quaternion.Normalize(Rotation);
        var m = Matrix4x4.CreateFromQuaternion(q);

        m.M41 = Position.X;
        m.M42 = Position.Y;
        m.M43 = Position.Z;

        return m;
    }
}