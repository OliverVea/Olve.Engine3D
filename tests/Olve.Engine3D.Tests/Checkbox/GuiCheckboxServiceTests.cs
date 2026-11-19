using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.GUI;
using Olve.Engine3D.GUI.Collision;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Input;
using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Input;
using Olve.Engine3D.Utilities;
using Olve.Utilities.Ids;
using System.Numerics;
using Olve.Engine3D.Math;

using static Olve.Engine3D.GUI.Elements.GuiCheckboxService;

namespace Olve.Engine3D.Tests.Checkbox;

public class GuiCheckboxServiceTests
{
    private record TestHarness(
        GuiNodeService NodeService,
        GuiAnchorService AnchorService,
        GuiElementService ElementService,
        GuiLayoutService LayoutService,
        GuiCollisionService CollisionService,
        GuiDepthService DepthService,
        GuiFocusService FocusService,
        GuiActivationService ActivationService,
        GuiNodeStateService StateService,
        GuiMouseInputService MouseInputService,
        GuiCheckboxService CheckboxService,
        PlatformEvents Events,
        InputState Input);

    private static TestHarness BuildHarness(params Pointer[] pointers)
    {
        var nodeService = new GuiNodeService(NullLogger<GuiNodeService>.Instance);
        var anchorService = new GuiAnchorService(NullLogger<GuiAnchorService>.Instance);
        var layoutContextService = new LayoutContextService(new TestWindowInfo());
        var elementService = new GuiElementService(nodeService);
        var stateService = new GuiNodeStateService(
            NullLogger<GuiNodeStateService>.Instance, nodeService, elementService);
        var layoutService = new GuiLayoutService(
            NullLogger<GuiLayoutService>.Instance, nodeService, stateService, anchorService, layoutContextService);
        var collisionService = new GuiCollisionService(layoutService);
        var depthService = new GuiDepthService(NullLogger<GuiDepthService>.Instance, nodeService, anchorService);
        var focusService = new GuiFocusService();
        var activationService = new GuiActivationService();

        var events = new PlatformEvents();
        var inputState = new InputState(new TestWindowInfo(), pointers.Length > 0 ? pointers : [Pointer.Mouse]);

        var mouseInputService = new GuiMouseInputService(
            inputState, collisionService, activationService,
            elementService, depthService, focusService, stateService);

        var checkboxService = new GuiCheckboxService(
            NullLogger<GuiCheckboxService>.Instance,
            elementService, activationService, stateService);

        return new TestHarness(
            nodeService, anchorService, elementService, layoutService,
            collisionService, depthService, focusService, activationService,
            stateService, mouseInputService, checkboxService, events, inputState);
    }

    private static (Engine3D.GUI.Elements.Checkbox Checkbox, Id<GuiElementRegistrations> RegistrationId) RegisterCheckbox(
        TestHarness h, bool initialValue = false)
    {
        var checkbox = new Engine3D.GUI.Elements.Checkbox
        {
            Id = Id.New<GuiElement>(),
            Name = "TestCheckbox",
            IsChecked = initialValue,
        };

        var anchorId = h.AnchorService.RegisterAnchor(AnchorPosition.TopLeft, GrowthDirection.DownRight).Value;

        h.LayoutService.Load();
        h.DepthService.Load();
        h.CheckboxService.Load();

        var registrationId = h.ElementService.RegisterElementAndChildren(anchorId, checkbox).Value;

        SetLayoutBoxesForElement(h, checkbox, registrationId);
        h.LayoutService.ComputeLayout();

        // Enable the background so mouse input recognises it
        if (h.ElementService.TryGetGuiNodeId(checkbox.Background.Id, registrationId, out var bgNodeId))
        {
            h.StateService.SetState(bgNodeId, GuiNodeState.Show | GuiNodeState.Enabled);
        }

        // Set initial indicator visibility
        if (h.ElementService.TryGetGuiNodeId(checkbox.Indicator.Id, registrationId, out var indicatorNodeId))
        {
            var indicatorState = initialValue
                ? GuiNodeState.Show | GuiNodeState.Enabled
                : GuiNodeState.Enabled;
            h.StateService.SetState(indicatorNodeId, indicatorState);
        }

        return (checkbox, registrationId);
    }

    private static void SetLayoutBoxesForElement(
        TestHarness h, GuiElement element, Id<GuiElementRegistrations> registrationId)
    {
        if (element.LayoutBox is { } layoutBox
            && h.ElementService.TryGetGuiNodeId(element.Id, registrationId, out var nodeId))
        {
            h.LayoutService.SetNodeBox(nodeId, layoutBox);
        }

        foreach (var child in element.Children)
        {
            SetLayoutBoxesForElement(h, child, registrationId);
        }
    }

    /// <summary>
    /// Presses the left button in one input frame and releases it in the next, at the same position.
    /// </summary>
    private static void SimulateClick(TestHarness h, float x, float y)
    {
        h.Events.AddAxis(MouseAxis.Position, new Vector2(x, y));
        h.Events.AddButtonDown(MouseButton.Left);
        RunInputFrame(h);

        h.Events.AddButtonUp(MouseButton.Left);
        RunInputFrame(h);

        h.CheckboxService.Update();
    }

    private static void RunInputFrame(TestHarness h)
    {
        h.Input.Apply(h.Events.Take());
        h.MouseInputService.Input();
    }

    [Test, NotInParallel]
    public async Task Checkbox_ValueChanged_Names_The_Mounted_Copy()
    {
        var h = BuildHarness();
        var (checkbox, registrationId) = RegisterCheckbox(h);
        CheckboxValueChangedMessage? received = null;
        h.CheckboxService.OnValueChanged.Subscribe(message => received = message);

        h.ElementService.TryGetGuiNodeId(checkbox.Background.Id, registrationId, out var backgroundNodeId);
        h.ActivationService.Activate(backgroundNodeId);

        await Assert.That(received?.RegistrationId).IsEqualTo(registrationId);
    }

    [Test, NotInParallel]
    public async Task Checkbox_ClickTogglesValue()
    {
        var h = BuildHarness();
        var (checkbox, _) = RegisterCheckbox(h, initialValue: false);

        GuiCheckboxService.CheckboxValueChangedMessage? captured = null;
        h.CheckboxService.OnValueChanged.Subscribe(msg => captured = msg);

        // Click center of checkbox (20x20 at top-left)
        SimulateClick(h, 10f, 10f);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value.IsChecked).IsTrue();
        await Assert.That(checkbox.IsChecked).IsTrue();
    }

    [Test, NotInParallel]
    public async Task Checkbox_PressAndReleaseInOneFrame_TogglesValue()
    {
        var h = BuildHarness();
        var (checkbox, _) = RegisterCheckbox(h, initialValue: false);

        h.Events.AddAxis(MouseAxis.Position, new Vector2(10f, 10f));
        h.Events.AddButtonDown(MouseButton.Left);
        h.Events.AddButtonUp(MouseButton.Left);
        RunInputFrame(h);
        h.CheckboxService.Update();

        await Assert.That(checkbox.IsChecked).IsTrue();
        await Assert.That(h.Input.WasPressed(Pointer.Mouse.Primary)).IsFalse();
    }

    [Test, NotInParallel]
    public async Task Checkbox_ClickOutside_LeavesPrimaryButtonForTheWorld()
    {
        var h = BuildHarness();
        RegisterCheckbox(h, initialValue: false);

        h.Events.AddAxis(MouseAxis.Position, new Vector2(100f, 100f));
        h.Events.AddButtonDown(MouseButton.Left);
        RunInputFrame(h);

        await Assert.That(h.Input.WasPressed(Pointer.Mouse.Primary)).IsTrue();
    }

    [Test, NotInParallel]
    public async Task Checkbox_DoubleClickReturnsToOriginal()
    {
        var h = BuildHarness();
        var (checkbox, _) = RegisterCheckbox(h, initialValue: false);

        SimulateClick(h, 10f, 10f);
        await Assert.That(checkbox.IsChecked).IsTrue();

        SimulateClick(h, 10f, 10f);
        await Assert.That(checkbox.IsChecked).IsFalse();
    }

    [Test, NotInParallel]
    public async Task Checkbox_InitiallyChecked_ClickUnchecks()
    {
        var h = BuildHarness();
        var (checkbox, _) = RegisterCheckbox(h, initialValue: true);

        SimulateClick(h, 10f, 10f);

        await Assert.That(checkbox.IsChecked).IsFalse();
    }

    [Test, NotInParallel]
    public async Task Checkbox_ClickOutside_DoesNotToggle()
    {
        var h = BuildHarness();
        var (checkbox, _) = RegisterCheckbox(h, initialValue: false);

        // Click outside the checkbox bounds
        SimulateClick(h, 100f, 100f);

        await Assert.That(checkbox.IsChecked).IsFalse();
    }

    [Test, NotInParallel]
    public async Task Checkbox_EventContainsCorrectId()
    {
        var h = BuildHarness();
        var (checkbox, _) = RegisterCheckbox(h, initialValue: false);

        GuiCheckboxService.CheckboxValueChangedMessage? captured = null;
        h.CheckboxService.OnValueChanged.Subscribe(msg => captured = msg);

        SimulateClick(h, 10f, 10f);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!.Value.CheckboxId).IsEqualTo(checkbox.Id);
    }


    // A second pointer on the same position axis, acting with the middle button.
    private static readonly Pointer SecondPointer = new(Id.FromName<Pointer>("Second"), MouseAxis.Position, MouseButton.Middle, MouseButton.Right);

    [Test, NotInParallel]
    public async Task Pointers_Press_And_Release_Independently()
    {
        var h = BuildHarness(Pointer.Mouse, SecondPointer);
        var (checkbox, _) = RegisterCheckbox(h, initialValue: false);

        // The mouse pointer presses outside and keeps holding; the second pointer clicks the checkbox.
        h.Events.AddAxis(MouseAxis.Position, new Vector2(100f, 100f));
        h.Events.AddButtonDown(MouseButton.Left);
        h.Events.AddAxis(MouseAxis.Position, new Vector2(10f, 10f));
        h.Events.AddButtonDown(MouseButton.Middle);
        h.Events.AddButtonUp(MouseButton.Middle);
        RunInputFrame(h);
        h.CheckboxService.Update();

        await Assert.That(checkbox.IsChecked).IsTrue();
        await Assert.That(h.Input.IsDown(MouseButton.Left)).IsTrue();
    }

    [Test, NotInParallel]
    public async Task Holding_An_Element_Consumes_Only_That_Pointers_Button()
    {
        var h = BuildHarness(Pointer.Mouse, SecondPointer);
        RegisterCheckbox(h, initialValue: false);

        // The mouse pointer holds the checkbox; the second pointer presses outside any element.
        h.Events.AddAxis(MouseAxis.Position, new Vector2(10f, 10f));
        h.Events.AddButtonDown(MouseButton.Left);
        h.Events.AddAxis(MouseAxis.Position, new Vector2(100f, 100f));
        h.Events.AddButtonDown(MouseButton.Middle);
        RunInputFrame(h);

        await Assert.That(h.Input.IsConsumed(MouseButton.Left)).IsTrue();
        await Assert.That(h.Input.WasPressed(MouseButton.Middle)).IsTrue();
    }

    [Test]
    public async Task Pointer_Position_Must_Be_An_Absolute_Axis()
    {
        await Assert.That(() => new Pointer(Id.FromName<Pointer>("Scroll"), MouseAxis.Scroll, MouseButton.Left, MouseButton.Right)).Throws<ArgumentException>();
    }

    [Test, NotInParallel]
    public async Task Disabled_Checkbox_Blocks_The_Click_Without_Pressing_Or_Toggling()
    {
        var h = BuildHarness();
        var (checkbox, registrationId) = RegisterCheckbox(h, initialValue: false);
        h.ElementService.TryGetGuiNodeId(checkbox.Background.Id, registrationId, out var bgNodeId);
        h.StateService.SetState(bgNodeId, GuiNodeState.Show);

        var pressedNodes = new List<Id<GuiNode>>();
        h.MouseInputService.OnPressedNode.Subscribe(pressedNodes.Add);

        h.Events.AddAxis(MouseAxis.Position, new Vector2(10f, 10f));
        h.Events.AddButtonDown(MouseButton.Left);
        h.Events.AddButtonUp(MouseButton.Left);
        RunInputFrame(h);
        h.CheckboxService.Update();

        await Assert.That(pressedNodes).IsEmpty();
        await Assert.That(checkbox.IsChecked).IsFalse();
        await Assert.That(h.Input.IsConsumed(MouseButton.Left)).IsTrue();
    }
}
