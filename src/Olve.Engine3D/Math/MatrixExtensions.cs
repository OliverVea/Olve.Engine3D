namespace Olve.Engine3D.Math;

public static class MatrixExtensions
{
    public static Result<Matrix4x4> Invert(this Matrix4x4 matrix)
    {
        return Matrix4x4.Invert(matrix, out var output)
            ? Result<Matrix4x4>.Success(output)
            : new ResultProblem("Failed to invert matrix '{0}'", matrix);
    }

    public static Matrix4x4 ExtractRotation(this Matrix4x4 matrix)
    {
        return new Matrix4x4(
            matrix.M11, matrix.M12, matrix.M13, 0,
            matrix.M21, matrix.M22, matrix.M23, 0,
            matrix.M31, matrix.M32, matrix.M33, 0,
            0, 0, 0, matrix.M44);
    }
}
