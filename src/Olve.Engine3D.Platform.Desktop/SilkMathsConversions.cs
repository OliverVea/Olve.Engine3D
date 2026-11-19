using Silk.NET.Maths;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>Converts between the engine's vectors and the Silk.NET.Maths vectors Silk.NET.Windowing uses.</summary>
internal static class SilkMathsConversions
{
    public static Vector2D<int> ToSilk(this Vector2<int> vector) => new(vector.X, vector.Y);

    public static Vector2<int> ToEngine(this Vector2D<int> vector) => new(vector.X, vector.Y);
}
