namespace Olve.Engine3D.GUI.Layout;

public readonly record struct BoxBounds(Vector2<Dp> Position, Vector2<Dp> Size)
{
    public Dp Width => Size.X;
    public Dp Height => Size.Y;
}