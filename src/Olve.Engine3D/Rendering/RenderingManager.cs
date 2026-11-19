using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.Rendering;

/// <summary>Orders the passes and their draws and hands each frame to the backend, the textures that follow the screen resized to it first; the order is worked out again only after a pass or draw is registered or deleted.</summary>
public class RenderingManager(IFrameBackend frameBackend, PassManager passManager, DrawManager drawManager, TextureManager textureManager)
{
    private Frame? _frame;
    private (int Passes, int Draws) _versions;

    public Result Render()
    {
        if (textureManager.FollowScreen().TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to resize the textures that follow the screen");
        }

        var versions = (passManager.Version, drawManager.Version);
        if (_frame is null || versions != _versions)
        {
            // Passes without draws still run, so their attachments are cleared.
            _frame = new Frame(
            [
                .. passManager.GetOrderedPasses().Select(passId => new FramePass(passId, drawManager.GetOrderedDraws(passId))),
            ]);
            _versions = versions;
        }

        return frameBackend.RenderFrame(_frame);
    }
}
