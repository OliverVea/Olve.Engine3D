using MemoryPack;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Assets.Entities;

/// <summary>
/// A texture asset. Its pixels are already in their GPU format, so it is registered as it is.
/// </summary>
[MemoryPackable]
public partial class TextureData<TFormat> : TextureBuffers<TFormat> where TFormat : unmanaged, ITextureFormat;
