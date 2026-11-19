using Olve.Engine3D.GUI.Collision;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Input;
using Olve.Engine3D.Scenes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.GUI.Input;

/// <summary>
/// Hit-tests the platform's pointers against interactive GUI elements: focus, press, release and activation. Each
/// pointer's primary button is consumed from a press on an element until its release, so the world doesn't see it.
/// Hover sets the focus when the main pointer moves or the wheel turns, so a focus set otherwise stays until then.
/// </summary>
public class GuiMouseInputService(
    InputState inputState,
    GuiCollisionService guiCollisionService,
    GuiActivationService guiActivationService,
    GuiElementService guiElementService,
    GuiDepthService guiDepthService,
    GuiFocusService guiFocusService,
    GuiNodeStateService stateService) : ISceneService
{
    private readonly Dictionary<Id<Pointer>, Id<GuiNode>> _pressedNodes = [];

    public Event<Id<GuiNode>> OnPressedNode { get; } = new();
    public Event<Id<GuiNode>> OnReleasedNode { get; } = new();

    public int Priority => -100;

    public Result Input()
    {
        // Only a moving pointer, or the wheel under the main mouse, moves the focus, so a menu appearing under a resting
        // one keeps its selection
        var main = inputState.MainPointer;
        var wheelTurned = main.Id == Pointer.Mouse.Id && inputState.GetValue(MouseAxis.Scroll) != Vector2.Zero;
        if (inputState.GetDelta(main.Position) != Vector2.Zero || wheelTurned)
        {
            guiFocusService.SetFocus(GetTopmostAt(inputState.GetPosition(main)));
        }

        foreach (var pointer in inputState.Pointers)
        {
            HandlePointer(pointer);
        }

        return Result.Success();
    }

    private void HandlePointer(Pointer pointer)
    {
        // A button another service took first (GuiNavigationService's activate key) presses nothing here
        if (inputState.IsConsumed(pointer.Primary) && !_pressedNodes.ContainsKey(pointer.Id))
        {
            return;
        }

        var consume = _pressedNodes.ContainsKey(pointer.Id);

        // Walk the frame's events in order, so each press and release is hit-tested where the pointer was then.
        var position = inputState.GetPreviousValue(pointer.Position);
        foreach (var platformEvent in inputState.Events)
        {
            position = GetPositionAfter(pointer, platformEvent, position);
            consume |= HandlePrimaryButton(pointer, platformEvent, position);
        }

        if (consume)
        {
            inputState.Consume(pointer.Primary);
        }
    }

    private static Vector2 GetPositionAfter(Pointer pointer, PlatformEvent platformEvent, Vector2 position)
    {
        if (platformEvent is not AxisEvent moved || moved.Axis != pointer.Position)
        {
            return position;
        }

        return moved.Value;
    }

    /// <returns>Whether the event pressed or released an element with the pointer's primary button.</returns>
    private bool HandlePrimaryButton(Pointer pointer, PlatformEvent platformEvent, Vector2 position) =>
        platformEvent switch
        {
            ButtonDownEvent down when down.Button == pointer.Primary => OnPrimaryDown(pointer, position),
            ButtonUpEvent up when up.Button == pointer.Primary => OnPrimaryUp(pointer, position),
            _ => false,
        };

    private Id<GuiNode>? GetTopmostAt(Vector2 position)
    {
        var collisions = guiCollisionService.GetGuiCollisions(position.ToPx());
        var interactive = collisions.Where(IsInteractive).ToList();

        if (interactive.Count == 0)
        {
            return null;
        }

        return interactive.MaxBy(guiDepthService.GetDepth);
    }

    private bool IsInteractive(Id<GuiNode> nodeId) =>
        guiElementService.TryGetElement(nodeId, out var element) && element.Interactive;

    /// <returns>Whether the press landed on an element.</returns>
    private bool OnPrimaryDown(Pointer pointer, Vector2 position)
    {
        if (GetTopmostAt(position) is not { } pressedNode)
        {
            return false;
        }

        _pressedNodes[pointer.Id] = pressedNode;

        // A disabled element holds the press, so the world doesn't get it, but isn't pressed.
        if (IsEnabled(pressedNode))
        {
            OnPressedNode.Invoke(pressedNode);
        }

        return true;
    }

    /// <returns>Whether a release at the position activates the node: it's still under the pointer and enabled.</returns>
    private bool ActivatesOnRelease(Id<GuiNode> pressedNode, Vector2 position)
    {
        if (GetTopmostAt(position) != pressedNode)
        {
            return false;
        }

        return IsEnabled(pressedNode);
    }

    private bool IsEnabled(Id<GuiNode> nodeId) =>
        stateService.TryGetState(nodeId, out var state) && state.HasFlag(GuiNodeState.Enabled);

    /// <returns>Whether the release ended a press on an element.</returns>
    private bool OnPrimaryUp(Pointer pointer, Vector2 position)
    {
        if (!_pressedNodes.Remove(pointer.Id, out var pressedNode))
        {
            return false;
        }

        if (ActivatesOnRelease(pressedNode, position))
        {
            guiActivationService.Activate(pressedNode);
        }

        OnReleasedNode.Invoke(pressedNode);
        return true;
    }
}
