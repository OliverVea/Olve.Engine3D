using Microsoft.Extensions.Logging;
using Olve.Engine3D.Graphics.WebGPU;
using Olve.WebGPU;

namespace Olve.Engine3D.Platform.Desktop;

public sealed class SurfacePresenter(ILogger<SurfacePresenter> logger) : IScreenPresenter
{
    public void Present(Surface surface)
    {
        if (surface.Present() != Status.Success)
        {
            logger.LogWarning("Failed to present the surface's texture");
        }
    }
}
