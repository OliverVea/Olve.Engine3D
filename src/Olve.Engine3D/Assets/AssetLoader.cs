using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Assets;

/// <summary>Reads assets from the <see cref="IAssetSource"/> and deserializes them.</summary>
public class AssetLoader(IAssetSource assetSource, ILogger<AssetLoader> logger)
{
    public async ValueTask<Result<T>> LoadAssetAsync<T>(AssetPath<T> assetPath, CancellationToken cancellationToken)
    {
        logger.LogDebug("Loading asset '{AssetPath}' of type '{TypeName}'", assetPath.Path.Path, typeof(T).Name);

        if ((await assetSource.ReadAsync(assetPath.Path, cancellationToken))
            .TryPickProblems(out var problems, out var bytes))
        {
            return problems.Prepend("Could not read asset '{0}' of type '{1}'", assetPath.Name, typeof(T).Name);
        }

        try
        {
            if (MemoryPackSerializer.Deserialize<T>(bytes) is not { } asset)
            {
                return new ResultProblem("Could not deserialize asset '{0}' of type '{1}' at '{2}'",
                    assetPath.Name, typeof(T).Name, assetPath.Path.Path);
            }

            logger.LogInformation("Loaded asset '{AssetPath}' of type '{TypeName}'", assetPath.Path.Path, typeof(T).Name);
            return asset;
        }
        catch (Exception e)
        {
            return new ResultProblem(e, "Got unexpected error while reading asset '{0}' of type '{1}'",
                assetPath.Path.Path, typeof(T).Name);
        }
    }
}
