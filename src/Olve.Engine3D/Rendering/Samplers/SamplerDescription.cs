namespace Olve.Engine3D.Rendering.Samplers;

public enum TextureFilter
{
    Nearest,
    Linear,
}

public enum TextureWrap
{
    Repeat,
    ClampToEdge,
}

/// <summary>
/// How a sampler reads a texture. A <see cref="ISamplerKind.Comparison"/> sampler passes where the reference is less than or equal to the texel.
/// </summary>
public readonly record struct SamplerDescription(TextureFilter Filter, TextureWrap Wrap);
