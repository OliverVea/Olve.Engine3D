namespace Olve.Engine3D.Math;

public readonly record struct Rectangle(Vector2 Origin, Vector2 Size)
{
    public Vector2 Max => Origin + Size;
    public Vector2 Center => Origin + Size / 2;
    public Vector2 HalfSize => Size / 2;
}
