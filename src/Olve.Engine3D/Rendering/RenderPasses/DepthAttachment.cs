using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>A texture a pass tests and writes depth in.</summary>
public sealed class DepthAttachment
{
    internal DepthAttachment(TextureId texture, Load<ITextureFormat.Depth> load)
    {
        Texture = texture;
        Load = load;
    }

    public TextureId Texture { get; }
    public Load<ITextureFormat.Depth> Load { get; }

    public static DepthAttachment Of<TUsage>(TextureId<ITextureFormat.Depth, TUsage> texture, Load<ITextureFormat.Depth> load)
        where TUsage : ITextureUsage.IRenderable => new(texture, load);

    public static DepthAttachment Clear<TUsage>(TextureId<ITextureFormat.Depth, TUsage> texture, ITextureFormat.Depth value)
        where TUsage : ITextureUsage.IRenderable => new(texture, new Load<ITextureFormat.Depth>.Clear(value));

    public static DepthAttachment Keep<TUsage>(TextureId<ITextureFormat.Depth, TUsage> texture)
        where TUsage : ITextureUsage.IRenderable => new(texture, new Load<ITextureFormat.Depth>.Keep());
}
