namespace Olve.Engine3D.GUI.Layout;

public static class PxExtensions
{
    /// <summary>A position in pixels, truncated to whole pixels.</summary>
    public static Vector2<Px> ToPx(this Vector2 position) =>
        new(new Px((int)position.X), new Px((int)position.Y));
}
