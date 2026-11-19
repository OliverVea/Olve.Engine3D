using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.GUI;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Input;
using Olve.Engine3D.GUI.Layout;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Gui;

public class GuiActivationHandlersTests
{
    private readonly GuiElementService _elements = new(new GuiNodeService(NullLogger<GuiNodeService>.Instance));
    private readonly GuiAnchorService _anchors = new(NullLogger<GuiAnchorService>.Instance);
    private readonly GuiActivationService _activation = new();
    private readonly Box _button = new() { Id = Id.FromName<GuiElement>("Panel/Box/Button"), Name = "Button" };

    private Id<GuiElementRegistrations> Mount()
    {
        _anchors.RegisterAnchor(AnchorPosition.Center, GrowthDirection.Center).TryPickProblems(out _, out var anchorId);
        _elements.RegisterElementAndChildren(anchorId, _button).TryPickProblems(out _, out var registrationId);
        return registrationId;
    }

    private void Activate(Id<GuiElementRegistrations> registrationId)
    {
        _elements.TryGetGuiNodeId(_button.Id, registrationId, out var nodeId);
        _activation.Activate(nodeId);
    }

    [Test]
    public async Task Two_Mounted_Copies_Of_A_Layout_Do_Not_Share_Clicks()
    {
        var handlers = new GuiActivationHandlers(_elements, _activation);
        var first = Mount();
        var second = Mount();
        var firstClicks = 0;
        var secondClicks = 0;
        await Assert.That(handlers.OnActivated(first, _button, () => firstClicks++).TryPickProblems(out _)).IsFalse();
        await Assert.That(handlers.OnActivated(second, _button, () => secondClicks++).TryPickProblems(out _)).IsFalse();

        Activate(second);

        await Assert.That((firstClicks, secondClicks)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task A_Handler_Goes_With_Its_Registration()
    {
        var handlers = new GuiActivationHandlers(_elements, _activation);
        var registration = Mount();
        var clicks = 0;
        handlers.OnActivated(registration, _button, () => clicks++).TryPickProblems(out _);
        _elements.TryGetGuiNodeId(_button.Id, registration, out var nodeId);

        _elements.UnregisterElementAndChildren(registration).TryPickProblems(out _);
        _activation.Activate(nodeId);

        await Assert.That(clicks).IsEqualTo(0);
    }

    [Test]
    public async Task An_Element_That_Is_Not_Mounted_Is_A_Problem()
    {
        var handlers = new GuiActivationHandlers(_elements, _activation);

        var result = handlers.OnActivated(Id.New<GuiElementRegistrations>(), _button, () => { });

        await Assert.That(result.TryPickProblems(out _)).IsTrue();
    }
}
