using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.GUI.Elements;

/// <summary>A square, single-colour icon read from a distance field atlas, so it stays sharp at any size.</summary>
public class Icon : GuiElement
{
    public required TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.Uploaded> Texture { get; set; }

    /// <summary>The icon's cell in the atlas, in pixels from the top left.</summary>
    public required Rectangle SourceRect { get; set; }

    public required Vector2<int> TextureSize { get; set; }
    public RGBA Color { get; set; } = RGBA.White;

    /// <summary>The side length; unset, the icon fills the space it's given.</summary>
    public int? Size { get; set; }

    public override LayoutBox? LayoutBox => new LayoutBox
    {
        Size = new SizeSpec(Dp.FromNullable(Size), Dp.FromNullable(Size), Size is null ? 1f : 0f, AspectRatio: 1f)
    };
}
