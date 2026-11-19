using Olve.Engine3D.Input.Actions;
using Olve.Engine3D.Scenes;

namespace Olve.Engine3D;

public class ScreenshotService(InputActionManager inputActionManager, ScreenshotManager screenshotManager) : ISceneService
{
    private static readonly IPath ScreenshotsFolder = Path.Create("screenshots");

    public int Priority => int.MaxValue;

    public Result Input()
    {
        if (inputActionManager.TryConsume(EngineInputActions.Screenshot))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var folder = ScreenshotsFolder / timestamp;
            screenshotManager.RequestFramebufferDump(folder);
        }

        return Result.Success();
    }
}
