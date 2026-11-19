namespace Olve.Engine3D.Math;

public static class Vector3Extensions
{
    public static int CountZeroDimensions(this Vector3 vec)
    {
        var count = 0;
        if (IsZero(vec.X)) count++;
        if (IsZero(vec.Y)) count++;
        if (IsZero(vec.Z)) count++;
        return count;
    }

    private static bool IsZero(float value)
    {
        return float.Abs(value) < Epsilon;
    }
}