namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>
/// The kind of sampler a texture is read through, matching WebGPU's sampler binding types.
/// </summary>
public interface ISamplerKind
{
    /// <summary>A sampler that may filter (blend neighbouring texels).</summary>
    public sealed class Filtering : ISamplerKind;

    /// <summary>A sampler that never filters; the only kind an unfilterable format such as <see cref="Textures.ITextureFormat.R32Float"/> accepts.</summary>
    public sealed class NonFiltering : ISamplerKind;

    /// <summary>A sampler that compares a reference value with the texels (shadow lookups in a <see cref="Textures.ITextureFormat.Depth"/> texture).</summary>
    public sealed class Comparison : ISamplerKind;
}

