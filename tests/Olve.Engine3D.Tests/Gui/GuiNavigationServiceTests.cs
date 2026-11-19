using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Input.Actions;
using Olve.Engine3D.GUI;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Input;
using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Utilities;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Gui;

public class GuiNavigationServiceTests
{
    private readonly GuiNodeService _nodes = new(NullLogger<GuiNodeService>.Instance);
    private readonly GuiAnchorService _anchors = new(NullLogger<GuiAnchorService>.Instance);
    private readonly GuiFocusService _focus = new();
    private readonly GuiActivationService _activation = new();

    private static Box Button(string name) => new() { Id = Id.FromName<GuiElement>($"Menu/Box/{name}"), Name = name, Interactive = true };

    [Test]
    public async Task The_First_Button_Is_The_First_Interactive_Leaf_In_Layout_Order()
    {
        var elements = new GuiElementService(_nodes);
        var states = new GuiNodeStateService(NullLogger<GuiNodeStateService>.Instance, _nodes, elements);
        var layout = new GuiLayoutService(NullLogger<GuiLayoutService>.Instance, _nodes, states, _anchors, new LayoutContextService(new TestWindowInfo()));
        var input = new InputState(new TestWindowInfo(), [Pointer.Mouse]);
        var actions = new InputActionManager(input, InputBindingOverrides.None.Resolve(EngineInputActions.ButtonActions), NullLogger<InputActionManager>.Instance);
        var navigation = new GuiNavigationService(_nodes, elements, states, layout, _focus, _activation, actions);

        // A scrim and a panel catch clicks, so they aren't buttons; the first button is the first leaf
        var first = Button("First");
        var second = Button("Second");
        var panel = new Box { Id = Id.FromName<GuiElement>("Menu/Box/Panel"), Name = "Panel", Interactive = true, Children = [first, second] };
        var scrim = new Box { Id = Id.FromName<GuiElement>("Menu/Box/Scrim"), Name = "Scrim", Interactive = true, Children = [panel] };

        _anchors.RegisterAnchor(AnchorPosition.Center, GrowthDirection.Center).TryPickProblems(out _, out var anchorId);
        elements.RegisterElementAndChildren(anchorId, scrim).TryPickProblems(out _, out var registrationId);
        foreach (var element in (GuiElement[])[scrim, panel, first, second])
        {
            elements.TryGetGuiNodeId(element.Id, registrationId, out var node);
            states.SetState(node, GuiNodeState.Show | GuiNodeState.Enabled);
        }

        elements.TryGetGuiNodeId(scrim.Id, registrationId, out var scrimNode);
        elements.TryGetGuiNodeId(first.Id, registrationId, out var firstNode);
        navigation.PushScope(scrimNode);

        await Assert.That(navigation.TryGetFirst(out var found)).IsTrue();
        await Assert.That(found).IsEqualTo(firstNode);
    }
}
