using Microsoft.Extensions.Logging;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Styling;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Systems;

namespace Olve.Engine3D.GUI.Layout;

public class GuiLayoutUpdateService(
    ILogger<GuiLayoutUpdateService> logger,
    EventQueueFactory eventQueueFactory,
    GuiElementService guiElementService,
    GuiLayoutService guiLayoutService,
    GuiStyleApplierService guiStyleApplierService
) : ISceneService
{
    private readonly EventQueue<GuiElementArgs> _elementRemovedQueue = eventQueueFactory.Create(guiElementService.OnRemoved);

    public int Priority => SceneServicePriority.FromDependents([ guiLayoutService ]);

    public Result Load()
    {
        guiElementService.OnAdded.Subscribe(OnAdded);
        _elementRemovedQueue.SetHandler(OnRemoved).Init();
        return Result.Success();
    }

    public Result Unload()
    {
        guiElementService.OnAdded.Unsubscribe(OnAdded);
        _elementRemovedQueue.Cleanup();
        return Result.Success();
    }

    public Result Update()
    {
        return Result.Chain(_elementRemovedQueue.Update, guiLayoutService.ComputeLayout);
    }

    private void OnAdded(GuiElementArgs args)
    {
        if (SetStyledBox(args).TryPickProblems(out var problems))
        {
            logger.LogError("Failed to lay out the added GUI element '{NodeId}': {Problems}", args.NodeId, problems);
        }
    }

    private Result SetStyledBox(GuiElementArgs args)
    {
        if (!guiElementService.TryGetElement(args.NodeId, out var element))
        {
            return new ResultProblem("Could not get the added GUI element");
        }

        if (guiStyleApplierService.ApplyCurrentState(args.NodeId).TryPickProblems(out var problems))
        {
            return problems;
        }

        if (element.LayoutBox is not { } layoutBox)
        {
            return Result.Success();
        }

        return guiLayoutService.SetNodeBox(args.NodeId, layoutBox);
    }

    private Result OnRemoved(GuiElementArgs args)
    {
        // guiLayoutService.RemoveBoxForNode(args.NodeId)
        return Result.Success();
    }
}
