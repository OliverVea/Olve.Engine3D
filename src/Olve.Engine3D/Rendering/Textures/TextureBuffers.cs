using System.Collections.Immutable;

namespace Olve.Engine3D.Rendering.Textures;

/// <summary>
/// A texture's pixels in their GPU format <typeparamref name="TFormat"/>, row by row from the bottom: what
/// <see cref="TextureManager"/> registers and the texture backend uploads.
/// </summary>
public class TextureBuffers<TFormat> where TFormat : unmanaged, ITextureFormat
{
    public required ImmutableArray<TFormat> Pixels { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
