namespace Olve.Engine3D.Input;

/// <summary>
/// Converts screen positions between pixels and normalized coordinates: -1..1 across the content area, with (0, 0)
/// at the centre and (-1, -1) at the top left.
/// </summary>
public static class ScreenPosition
{
    public static Vector2 ToNormalized(Vector2 position, Vector2<int> contentAreaSize) =>
        new(position.X / contentAreaSize.X * 2f - 1f, position.Y / contentAreaSize.Y * 2f - 1f);

    public static Vector2 FromNormalized(Vector2 normalizedPosition, Vector2<int> contentAreaSize) =>
        new((normalizedPosition.X + 1f) / 2f * contentAreaSize.X, (normalizedPosition.Y + 1f) / 2f * contentAreaSize.Y);
}
