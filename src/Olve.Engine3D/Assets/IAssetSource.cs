namespace Olve.Engine3D.Assets;

/// <summary>
/// Where the built assets come from: files next to the executable on desktop, fetched over the network in a browser.
/// The host registers it. Reads are asynchronous, so a browser can fetch.
/// </summary>
public interface IAssetSource
{
    /// <summary>The bytes of the asset at the path, relative to the assets root.</summary>
    ValueTask<Result<byte[]>> ReadAsync(IPurePath assetPath, CancellationToken cancellationToken);
}
