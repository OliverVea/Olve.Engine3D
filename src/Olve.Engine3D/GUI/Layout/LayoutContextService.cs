namespace Olve.Engine3D.GUI.Layout;

/// <summary>
/// The current <see cref="LayoutContext"/>, derived from the window's content area size. The design size matches the
/// content area, so at UI scale 1 one dp is one pixel.
/// </summary>
public sealed class LayoutContextService(IWindowInfo windowInfo)
{
    private const float UiScale = 1f;

    private Vector2<int> _size;
    private LayoutContext _current = Create(new Vector2<int>(1, 1));

    /// <summary>
    /// Follows the window size. A minimized window reports zero, which keeps the last context.
    /// </summary>
    public LayoutContext Current
    {
        get
        {
            var size = windowInfo.ContentAreaSize;
            if (size != _size && size.X > 0 && size.Y > 0)
            {
                _size = size;
                _current = Create(size);
            }

            return _current;
        }
    }

    private static LayoutContext Create(Vector2<int> size)
    {
        var aspectRatio = (float)size.X / size.Y;
        var designWidth = MathF.Round(size.X * UiScale);
        var designHeight = designWidth / aspectRatio;

        return new LayoutContext(
            new Vector2<Dp>(designWidth, designHeight),
            aspectRatio,
            new DpPxRatio(designWidth / size.X),
            UiScale);
    }
}
