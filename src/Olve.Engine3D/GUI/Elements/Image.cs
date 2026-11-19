using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.GUI.Elements;

public class Image : GuiElement, IRenderableAsRectangle
{
    public required TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.Uploaded> Texture { get; set; }
    public RGBA Tint { get; set; } = RGBA.White;
    public float Alpha { get; set; } = 1f;
    public float? AspectRatio { get; set; }
    public FitMode Fit { get; set; } = FitMode.Contain;
    public Rectangle? SourceRect { get; set; }
    public Vector2<int>? TextureSize { get; set; }

    public override LayoutBox? LayoutBox => new LayoutBox()
    {
        Size = new SizeSpec(null, null, 1f, AspectRatio, Fit)
    };

    public IRenderableAsRectangle.Data TexturedRectangleData
    {
        get
        {
            var uvMin = Vector2.Zero;
            var uvMax = Vector2.One;

            if (SourceRect is { } rect && TextureSize is { } size)
            {
                // Flip Y: atlas pixel coords have Y=0 at top, texture coordinates have Y=0 at bottom
                uvMin = new Vector2(rect.Origin.X / size.X, 1f - (rect.Origin.Y + rect.Size.Y) / size.Y);
                uvMax = new Vector2((rect.Origin.X + rect.Size.X) / size.X, 1f - rect.Origin.Y / size.Y);
            }

            return new()
            {
                Color = Tint,
                Texture = Texture,
                UvMin = uvMin,
                UvMax = uvMax,
            };
        }
    }
}
