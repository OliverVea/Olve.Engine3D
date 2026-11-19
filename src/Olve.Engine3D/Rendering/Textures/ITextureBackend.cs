using Olve.Utilities.IntegerMath2D;

namespace Olve.Engine3D.Rendering.Textures;

/// <summary>Creates, uploads and deletes textures on the GPU.</summary>
public interface ITextureBackend
{
    Result UploadTexture<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId, TextureBuffers<TFormat> textureData)
        where TFormat : unmanaged, ITextureFormat;

    /// <summary>Creates a texture passes draw into; multisampled, with as many samples per pixel as the screen (see <see cref="TextureManager.Create{TFormat, TUsage}"/>).</summary>
    Result CreateTexture<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId, Size textureSize, bool multisampled)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage.IRenderable;

    /// <summary>Makes a created texture again at a new size, as it was created otherwise; what it held is lost.</summary>
    Result ResizeTexture(TextureId textureId, Size textureSize);

    DeletionResult DeleteTexture(TextureId textureId);

    ValueTask<Result<TextureBuffers<TFormat>>> ReadTextureAsync<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId)
        where TFormat : unmanaged, ITextureFormat
        where TUsage : ITextureUsage.IRenderable;
}