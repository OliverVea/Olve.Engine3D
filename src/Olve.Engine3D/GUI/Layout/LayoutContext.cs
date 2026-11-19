namespace Olve.Engine3D.GUI.Layout;

public readonly record struct LayoutContext(
    Vector2<Dp> DesignSize,
    float AspectRatio,
    DpPxRatio DpPxRatio,
    float UiScale)
{
    public Dp ToDp(Px px) => DpPxRatio.ToDp(px);
    public Px ToPx(Dp dp) => DpPxRatio.ToPx(dp);

    public Vector2<Dp> ToDp(Vector2<Px> px) => new(DpPxRatio.ToDp(px.X), DpPxRatio.ToDp(px.Y));
    public Vector2<Px> ToPx(Vector2<Dp> dp) => new(DpPxRatio.ToPx(dp.X), DpPxRatio.ToPx(dp.Y));
}
