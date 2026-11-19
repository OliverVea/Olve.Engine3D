using Microsoft.Extensions.Logging;
using Olve.Engine3D.GUI.Collision;
using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Input;
using Olve.Engine3D.Scenes;

namespace Olve.Engine3D.GUI.Input;

public class GuiScrollService(
    ILogger<GuiScrollService> logger,
    InputState inputState,
    GuiCollisionService guiCollisionService,
    GuiLayoutService guiLayoutService,
    GuiDepthService guiDepthService) : ISceneService
{
    private static readonly Dp NotchLength = new(48);

    public int Priority => -150;

    public Result Input()
    {
        var scroll = inputState.GetValue(MouseAxis.Scroll);
        if (scroll == Vector2.Zero)
        {
            return Result.Success();
        }

        var position = inputState.GetPosition(Pointer.Mouse);
        var scrollBoxes = guiCollisionService
            .GetGuiCollisions(position.ToPx())
            .Where(guiLayoutService.IsScrollBox)
            .ToList();

        if (scrollBoxes.Count > 0)
        {
            var scrollBox = scrollBoxes.MaxBy(guiDepthService.GetDepth);
            guiLayoutService.ScrollBy(scrollBox, -scroll.Y * NotchLength);
            logger.LogDebug("Scrolled scroll box {ScrollBox} by {Notches} notches", scrollBox, scroll.Y);
        }

        return Result.Success();
    }
}
