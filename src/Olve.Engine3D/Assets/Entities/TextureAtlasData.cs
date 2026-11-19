using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Assets.Entities;

public class TextureAtlasData<TFormat> where TFormat : unmanaged, ITextureFormat
{
    public required AssetPath<TextureData<TFormat>> Texture { get; init; }
    public required Vector2<int> Size { get; init; }
}
