using Olve.Engine3D.GUI.Elements;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.GUI.Input;

/// <summary>
/// Calls a handler when one mounted element is activated: the element as registered under one registration, so two
/// mounted copies of a layout don't share clicks. A handler goes when its element is unregistered.
/// </summary>
public class GuiActivationHandlers
{
    private readonly GuiElementService _guiElementService;
    private readonly Dictionary<Id<GuiNode>, Action> _handlers = new();

    public GuiActivationHandlers(GuiElementService guiElementService, GuiActivationService guiActivationService)
    {
        _guiElementService = guiElementService;
        guiActivationService.GuiElementActivated.Subscribe(message => _handlers.GetValueOrDefault(message.NodeId)?.Invoke());
        guiElementService.OnRemoved.Subscribe(removed => _handlers.Remove(removed.NodeId));
    }

    /// <summary>Calls <paramref name="handler"/> when <paramref name="element"/>, mounted under <paramref name="registrationId"/>, is activated.</summary>
    public Result OnActivated(Id<GuiElementRegistrations> registrationId, GuiElement element, Action handler)
    {
        if (!_guiElementService.TryGetGuiNodeId(element.Id, registrationId, out var nodeId))
        {
            return new ResultProblem("Element '{0}' isn't mounted under registration '{1}'", element.Name, registrationId);
        }

        _handlers[nodeId] = handler;
        return Result.Success();
    }
}
