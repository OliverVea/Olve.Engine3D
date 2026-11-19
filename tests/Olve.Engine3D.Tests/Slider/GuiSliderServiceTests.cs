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

namespace Olve.Engine3D.Tests.Slider;

public class GuiSliderServiceTests
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
        GuiSliderService SliderService,
        PlatformEvents Events,
        InputState Input);

    private static TestHarness BuildHarness()
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
        var inputState = new InputState(new TestWindowInfo(), [Pointer.Mouse]);

        var mouseInputService = new GuiMouseInputService(
            inputState, collisionService, activationService,
            elementService, depthService, focusService, stateService);

        var sliderService = new GuiSliderService(
            NullLogger<GuiSliderService>.Instance,
            elementService, mouseInputService, layoutService,
            inputState, layoutContextService);

        return new TestHarness(
            nodeService, anchorService, elementService, layoutService,
            collisionService, depthService, focusService, activationService,
            stateService, mouseInputService, sliderService, events, inputState);
    }

    private static (Olve.Engine3D.GUI.Elements.Slider Slider, Id<GuiElementRegistrations> RegistrationId) RegisterSlider(
        TestHarness h, float minValue = 0f, float maxValue = 1f, float initialValue = 0f)
    {
        var slider = new Olve.Engine3D.GUI.Elements.Slider
        {
            Id = Id.New<GuiElement>(),
            Name = "TestSlider",
            MinValue = minValue,
            MaxValue = maxValue,
            Value = initialValue,
            Width = 200,
            Height = 20,
        };

        var anchorId = h.AnchorService.RegisterAnchor(AnchorPosition.TopLeft, GrowthDirection.DownRight).Value;

        // Load scene services that need it
        h.LayoutService.Load();
        h.DepthService.Load();
        h.SliderService.Load();

        var registrationId = h.ElementService.RegisterElementAndChildren(anchorId, slider).Value;

        // GuiLayoutUpdateService normally sets layout boxes via event queue;
        // in tests we replicate this manually for each registered element.
        SetLayoutBoxesForElement(h, slider, registrationId);

        // Force layout computation so collision detection works
        h.LayoutService.ComputeLayout();

        // Set Enabled state on the thumb and track so GuiMouseInputService recognises them
        if (h.ElementService.TryGetGuiNodeId(slider.Thumb.Id, registrationId, out var thumbNodeId))
        {
            h.StateService.SetState(thumbNodeId, GuiNodeState.Show | GuiNodeState.Enabled);
        }

        if (h.ElementService.TryGetGuiNodeId(slider.Track.Id, registrationId, out var trackNodeId))
        {
            h.StateService.SetState(trackNodeId, GuiNodeState.Show | GuiNodeState.Enabled);
        }

        return (slider, registrationId);
    }

    private static (Id<GuiElementRegistrations> RegistrationId, Id<GuiNode> ThumbNodeId) GetThumbNodeId(
        TestHarness h, Olve.Engine3D.GUI.Elements.Slider slider)
    {
        // Find any registration for this slider's thumb
        h.ElementService.TryGetAnyGuiNodeId(slider.Thumb.Id, out var thumbNodeId);
        h.ElementService.TryGetElementIds(thumbNodeId, out _, out var registrationId);
        return (registrationId, thumbNodeId);
    }

    /// <summary>
    /// Mirrors what GuiLayoutUpdateService does: for each element in the tree,
    /// sets the element's LayoutBox on the corresponding GuiNode.
    /// </summary>
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
    /// Moves the mouse to the given position and presses the left button, then runs one input frame:
    /// GuiMouseInputService detects the hit and fires OnPressedNode, then GuiSliderService processes the drag.
    /// </summary>
    private static void SimulateMousePress(TestHarness h, float x, float y)
    {
        h.Events.AddAxis(MouseAxis.Position, new Vector2(x, y));
        h.Events.AddButtonDown(MouseButton.Left);
        RunInputFrame(h);
    }

    /// <summary>
    /// Moves the mouse without pressing or releasing a button.
    /// </summary>
    private static void SimulateMouseMove(TestHarness h, float x, float y)
    {
        h.Events.AddAxis(MouseAxis.Position, new Vector2(x, y));
        RunInputFrame(h);
    }

    private static void SimulateMouseRelease(TestHarness h, float x, float y)
    {
        h.Events.AddAxis(MouseAxis.Position, new Vector2(x, y));
        h.Events.AddButtonUp(MouseButton.Left);
        RunInputFrame(h);
    }

    private static void RunInputFrame(TestHarness h)
    {
        h.Input.Apply(h.Events.Take());
        h.MouseInputService.Input();
        h.SliderService.Input();
    }

    [Test, NotInParallel]
    public async Task Slider_LayoutAndCollision_Diagnostic()
    {
        var h = BuildHarness();
        var (slider, registrationId) = RegisterSlider(h, minValue: 0f, maxValue: 100f, initialValue: 0f);

        // Check what positions the layout computed
        h.ElementService.TryGetGuiNodeId(slider.Id, registrationId, out var sliderNodeId);
        h.ElementService.TryGetGuiNodeId(slider.Track.Id, registrationId, out var trackNodeId);
        h.ElementService.TryGetGuiNodeId(slider.Thumb.Id, registrationId, out var thumbNodeId);

        var gotSlider = h.LayoutService.TryGetBoxPosition(sliderNodeId, out var sliderPos);
        var gotTrack = h.LayoutService.TryGetBoxPosition(trackNodeId, out var trackPos);
        var gotThumb = h.LayoutService.TryGetBoxPosition(thumbNodeId, out var thumbPos);

        // These should all be true
        await Assert.That(gotSlider).IsTrue();
        await Assert.That(gotTrack).IsTrue();
        await Assert.That(gotThumb).IsTrue();

        // Print diagnostic info via assertions
        // Slider should be 200x20
        await Assert.That(sliderPos.Size.X.Value).IsEqualTo(200);
        await Assert.That(sliderPos.Size.Y.Value).IsEqualTo(20);

        // Track should fill the slider width (200), height=4, centered vertically
        await Assert.That(trackPos.Size.X.Value).IsEqualTo(200);

        // Thumb should be 16x16
        await Assert.That(thumbPos.Size.X.Value).IsEqualTo(16);
        await Assert.That(thumbPos.Size.Y.Value).IsEqualTo(16);

        // Check collision at thumb position
        var collisions = h.CollisionService.GetGuiCollisions(
            new Vector2<Px>(new Px(8), new Px((int)thumbPos.Position.Y.Value + 8))).ToList();

        await Assert.That(collisions).IsNotEmpty();
    }

    [Test, NotInParallel]
    public async Task Slider_ClickAtCenter_SetsValueToMidpoint()
    {
        // Arrange
        var h = BuildHarness();
        var (slider, _) = RegisterSlider(h, minValue: 0f, maxValue: 100f, initialValue: 0f);

        // The slider is 200px wide at position (0,0).
        // Thumb is 16px wide by default. Usable range = 200 - 16 = 184px.
        // Click at center x=100 should give t = (100 - 0 - 8) / 184 = 92/184 = 0.5
        // Value = 0 + 0.5 * 100 = 50

        float? capturedValue = null;
        h.SliderService.OnValueChanged.Subscribe(msg => capturedValue = msg.NewValue);

        // Act — click on the thumb first (it starts at x=0..16), then drag to center
        SimulateMousePress(h, 8f, 10f); // press on thumb (centered at x=8)
        SimulateMouseMove(h, 100f, 10f); // drag to center

        // Assert
        await Assert.That(capturedValue).IsNotNull();
        await Assert.That(capturedValue!.Value).IsEqualTo(50f).Within(1f);
        await Assert.That(slider.Value).IsEqualTo(50f).Within(1f);
    }

    [Test, NotInParallel]
    public async Task Slider_DragToStart_SetsValueToMin()
    {
        var h = BuildHarness();
        var (slider, _) = RegisterSlider(h, minValue: 0f, maxValue: 100f, initialValue: 0f);

        // Thumb starts at left edge (x=0..16). Click it, drag to center, release.
        SimulateMousePress(h, 8f, 10f);
        SimulateMouseMove(h, 100f, 10f);
        SimulateMouseRelease(h, 100f, 10f);

        // Now thumb is at ~midpoint. Find its actual position.
        var (_, thumbNodeId) = GetThumbNodeId(h, slider);
        h.LayoutService.TryGetBoxPosition(thumbNodeId, out var thumbPos);
        var thumbCenterX = thumbPos.Position.X.Value + thumbPos.Size.X.Value / 2f;

        // Click the thumb at its current position, then drag to far left.
        float? capturedValue = null;
        h.SliderService.OnValueChanged.Subscribe(msg => capturedValue = msg.NewValue);

        SimulateMousePress(h, thumbCenterX, 10f);
        SimulateMouseMove(h, 0f, 10f);

        await Assert.That(capturedValue).IsNotNull();
        await Assert.That(capturedValue!.Value).IsEqualTo(0f).Within(1f);
    }

    [Test, NotInParallel]
    public async Task Slider_ClickAtEnd_SetsValueToMax()
    {
        var h = BuildHarness();
        var (slider, _) = RegisterSlider(h, minValue: 0f, maxValue: 100f, initialValue: 0f);

        float? capturedValue = null;
        h.SliderService.OnValueChanged.Subscribe(msg => capturedValue = msg.NewValue);

        // Thumb starts at x=0..16. Click thumb, drag to far right (x=200).
        SimulateMousePress(h, 8f, 10f);
        SimulateMouseMove(h, 200f, 10f);

        await Assert.That(capturedValue).IsNotNull();
        await Assert.That(capturedValue!.Value).IsEqualTo(100f).Within(1f);
    }

    [Test, NotInParallel]
    public async Task Slider_ReleaseStopsDrag()
    {
        var h = BuildHarness();
        var (slider, _) = RegisterSlider(h, minValue: 0f, maxValue: 100f, initialValue: 0f);

        // Click thumb, drag to center, release
        SimulateMousePress(h, 8f, 10f);
        SimulateMouseMove(h, 100f, 10f);
        var valueAfterDrag = slider.Value;

        SimulateMouseRelease(h, 100f, 10f);

        // Move mouse after release — value should NOT change
        SimulateMouseMove(h, 200f, 10f);
        var valueAfterRelease = slider.Value;

        await Assert.That(valueAfterDrag).IsEqualTo(valueAfterRelease);
    }

    [Test, NotInParallel]
    public async Task Slider_ClickTrackBar_SnapsThumbToPosition()
    {
        var h = BuildHarness();
        var (slider, _) = RegisterSlider(h, minValue: 0f, maxValue: 100f, initialValue: 0f);

        float? capturedValue = null;
        h.SliderService.OnValueChanged.Subscribe(msg => capturedValue = msg.NewValue);

        // Click directly on the track bar at x=100 (center of the 200px track).
        // The thumb starts at x=0..16 so x=100 is well outside it — this hits the track.
        // t = (100 - 0 - 8) / (200 - 16) = 92/184 = 0.5 → value ~50
        SimulateMousePress(h, 100f, 10f);

        await Assert.That(capturedValue).IsNotNull();
        await Assert.That(capturedValue!.Value).IsEqualTo(50f).Within(1f);
    }

    [Test, NotInParallel]
    public async Task Slider_ValueClampedToRange()
    {
        var h = BuildHarness();
        var (slider, _) = RegisterSlider(h, minValue: 10f, maxValue: 90f, initialValue: 10f);

        // Click thumb, drag way past the right edge
        SimulateMousePress(h, 8f, 10f);
        SimulateMouseMove(h, 9999f, 10f);

        await Assert.That(slider.Value).IsEqualTo(90f).Within(0.1f);

        // Now drag way past the left edge
        SimulateMouseMove(h, -9999f, 10f);

        await Assert.That(slider.Value).IsEqualTo(10f).Within(0.1f);
    }
}
