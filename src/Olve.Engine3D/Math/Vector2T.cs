namespace Olve.Engine3D.Math;

/// <summary>
/// A pair of integers or units (<see cref="GUI.Layout.Px"/>, <see cref="GUI.Layout.Dp"/>). Floats use
/// <see cref="System.Numerics.Vector2"/>, which the JIT compiles to SIMD instructions.
/// </summary>
public readonly record struct Vector2<T>(T X, T Y)
    where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>
{
    public static Vector2<T> operator +(Vector2<T> a, Vector2<T> b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2<T> operator -(Vector2<T> a, Vector2<T> b) => new(a.X - b.X, a.Y - b.Y);
}
