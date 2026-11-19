using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Rendering.Textures;

/// <summary>Reads textures' pixels: uploaded textures from their CPU copy, rendered ones back from the GPU.</summary>
public class TextureReadingManager(
    TextureManager textureManager,
    ITextureBackend textureBackend,
    ILogger<TextureReadingManager> logger)
{
    public ValueTask<Result<TextureBuffers<TFormat>>> ReadAsync<TFormat>(TextureId<TFormat, ITextureUsage.Uploaded> textureId)
        where TFormat : unmanaged, ITextureFormat
    {
        Result<TextureBuffers<TFormat>> result = textureManager.TryGetData(textureId, out var data)
            ? data
            : new ResultProblem("Texture '{0}' isn't registered", textureId);

        return ValueTask.FromResult(result);
    }

    public async ValueTask<Result<TextureBuffers<TFormat>>> ReadAsync<TFormat, TUsage>(TextureId<TFormat, TUsage> textureId)
        where TFormat : unmanaged, ITextureFormat
        where TUsage : ITextureUsage.IRenderable
    {
        logger.LogDebug("Reading texture {TextureId} back from the GPU", textureId);

        if ((await textureBackend.ReadTextureAsync(textureId)).TryPickProblems(out var problems, out var buffers))
        {
            return problems.Prepend("Failed to read texture '{0}' back from the GPU", textureId);
        }

        return buffers;
    }
}
