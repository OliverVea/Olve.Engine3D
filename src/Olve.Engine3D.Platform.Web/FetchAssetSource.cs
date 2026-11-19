using Olve.Engine3D.Assets;
using Olve.Paths;

namespace Olve.Engine3D.Platform.Web;

public sealed class FetchAssetSource(HttpClient httpClient) : IAssetSource
{
    public async ValueTask<Result<byte[]>> ReadAsync(IPurePath assetPath, CancellationToken cancellationToken)
    {
        var location = new Uri(httpClient.BaseAddress!, assetPath.Path);
        try
        {
            using var response = await httpClient.GetAsync(location, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ResultProblem("No asset at '{0}' ({1} {2})", location, (int)response.StatusCode, response.ReasonPhrase ?? "");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return new ResultProblem(ex, "Failed to fetch the asset at '{0}'", location);
        }
    }
}
