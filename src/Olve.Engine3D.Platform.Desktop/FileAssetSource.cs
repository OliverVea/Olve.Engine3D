using Olve.Engine3D.Assets;
using Olve.Paths;
using Path = Olve.Paths.Path;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>Reads assets from the <c>assets</c> folder next to the executable.</summary>
public sealed class FileAssetSource : IAssetSource
{
    private readonly IPath _assetsFolder = Path.TryGetAssemblyExecutable(out var executable)
        ? executable.Parent / "assets"
        : throw new InvalidOperationException("Could not get the executable's path");

    public async ValueTask<Result<byte[]>> ReadAsync(IPurePath assetPath, CancellationToken cancellationToken)
    {
        var location = _assetsFolder / assetPath.Path;
        if (!location.Exists())
        {
            return new ResultProblem("No asset at '{0}'", location.Path);
        }

        return await File.ReadAllBytesAsync(location.Path, cancellationToken);
    }
}
