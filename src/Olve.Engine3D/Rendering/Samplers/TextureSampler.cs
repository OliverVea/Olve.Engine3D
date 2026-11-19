using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>A texture as one shader input reads it: the texture and its sampler. Built with <see cref="TextureSampler.Of"/>.</summary>
public readonly record struct TextureSampler<TFormat, TKind>
    where TFormat : ITextureFormat, ISampledWith<TKind>
    where TKind : ISamplerKind
{
    internal TextureSampler(TextureId texture, SamplerId<TKind> sampler)
    {
        Texture = texture;
        Sampler = sampler;
    }

    public TextureId Texture { get; }
    public SamplerId<TKind> Sampler { get; }
}

public static class TextureSampler
{
    public static TextureSampler<TFormat, TKind> Of<TFormat, TUsage, TKind>(
        TextureId<TFormat, TUsage> texture,
        SamplerId<TKind> sampler)
        where TFormat : ITextureFormat, ISampledWith<TKind>
        where TUsage : ITextureUsage.ISampleable
        where TKind : ISamplerKind => new(texture, sampler);
}
