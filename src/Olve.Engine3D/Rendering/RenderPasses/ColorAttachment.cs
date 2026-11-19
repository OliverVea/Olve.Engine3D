using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>A texture a pass draws colour into.</summary>
public abstract class ColorAttachment
{
    private protected ColorAttachment(TextureId texture)
    {
        Texture = texture;
    }

    public TextureId Texture { get; }

    public static ColorAttachment<TFormat> Of<TFormat, TUsage>(TextureId<TFormat, TUsage> texture, Load<TFormat> load)
        where TFormat : ITextureFormat.IColor
        where TUsage : ITextureUsage.IRenderable => new(texture, load);

    public static ColorAttachment<TFormat> Clear<TFormat, TUsage>(TextureId<TFormat, TUsage> texture, TFormat value)
        where TFormat : ITextureFormat.IColor
        where TUsage : ITextureUsage.IRenderable => Of(texture, new Load<TFormat>.Clear(value));

    public static ColorAttachment<TFormat> Keep<TFormat, TUsage>(TextureId<TFormat, TUsage> texture)
        where TFormat : ITextureFormat.IColor
        where TUsage : ITextureUsage.IRenderable => Of(texture, new Load<TFormat>.Keep());
}

public sealed class ColorAttachment<TFormat> : ColorAttachment where TFormat : ITextureFormat.IColor
{
    internal ColorAttachment(TextureId texture, Load<TFormat> load) : base(texture)
    {
        Load = load;
    }

    public Load<TFormat> Load { get; }
}
