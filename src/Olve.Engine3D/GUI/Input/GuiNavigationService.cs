using Olve.Engine3D.Scenes;
using Olve.Engine3D.Input.Actions;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Layout;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.GUI.Input;

/// <summary>
/// Selects and presses the buttons of one tree, such as an open menu, with keys and gamepads (the GuiUp, GuiDown,
/// GuiLeft, GuiRight and GuiActivate actions), independent of any pointer: the selection is the GUI focus, and moves
/// to the nearest button in the direction by their layout. A button is an interactive, enabled element with no
/// interactive descendants, so a menu's scrim and background aren't. Runs before pointer input, which then doesn't
/// see the keys it took.
/// </summary>
public class GuiNavigationService(
    GuiNodeService guiNodeService,
    GuiElementService guiElementService,
    GuiNodeStateService stateService,
    GuiLayoutService guiLayoutService,
    GuiFocusService guiFocusService,
    GuiActivationService guiActivationService,
    InputActionManager inputActionManager) : ISceneService
{
    private static readonly (ButtonAction Action, Vector2 Direction)[] Directions =
    [
        (EngineInputActions.GuiUp, new Vector2(0, -1)),
        (EngineInputActions.GuiDown, new Vector2(0, 1)),
        (EngineInputActions.GuiLeft, new Vector2(-1, 0)),
        (EngineInputActions.GuiRight, new Vector2(1, 0)),
    ];

    // Before GuiMouseInputService (-100), so a pointer whose button is bound to GuiActivate doesn't press it too
    public int Priority => -200;

    // Off-axis distance counts this much more than distance along the direction, so moves stay in their lane
    private const float OffAxisWeight = 2f;

    private readonly Stack<(Id<GuiNode> Root, Id<GuiNode>? FocusBelow)> _scopes = new();

    private Id<GuiNode>? CurrentScope => _scopes.Count > 0 ? _scopes.Peek().Root : null;

    /// <summary>Whether navigation is limited to a tree, such as an open menu.</summary>
    public bool IsActive => _scopes.Count > 0;

    public void PushScope(Id<GuiNode> root) => _scopes.Push((root, guiFocusService.FocusedNode));

    public void PopScope()
    {
        if (_scopes.TryPop(out var popped))
        {
            guiFocusService.SetFocus(popped.FocusBelow is { } focus && guiElementService.TryGetElement(focus, out _) ? focus : null);
        }
    }

    public void ClearScopes() => _scopes.Clear();

    public Result Input()
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        foreach (var (action, direction) in Directions)
        {
            if (inputActionManager.TryConsume(action) && TryGetNext(direction, out var next))
            {
                guiFocusService.SetFocus(next);
                guiLayoutService.ScrollIntoView(next);
            }
        }

        if (inputActionManager.TryConsume(EngineInputActions.GuiActivate))
        {
            Activate();
        }

        return Result.Success();
    }

    /// <summary>Selects the scope's first button.</summary>
    public void FocusFirst()
    {
        if (TryGetFirst(out var first))
        {
            guiFocusService.SetFocus(first);
        }
    }

    /// <summary>The scope's first button, in layout order.</summary>
    public bool TryGetFirst(out Id<GuiNode> button)
    {
        var buttons = GetButtons();
        button = buttons.FirstOrDefault();
        return buttons.Count > 0;
    }

    /// <summary>
    /// The nearest button in <paramref name="direction"/> (screen space, y down) from the focused one, or the first
    /// button when none of them is focused. Along a scroll box's axis, the nearest in that box comes first, so the
    /// selection leaves a list only at its end; a button scrolled out of view is reached only from its own list.
    /// </summary>
    public bool TryGetNext(Vector2 direction, out Id<GuiNode> next)
    {
        var buttons = GetButtons();
        if (guiFocusService.FocusedNode is not { } current || !buttons.Contains(current)
            || !TryGetCentre(current, out var from))
        {
            return TryGetFirst(out next);
        }

        var along = Vector2.Normalize(direction);
        var inList = guiLayoutService.TryGetScrollBoxAround(current, out var list);
        bool InSameList(Id<GuiNode> button) =>
            inList && guiLayoutService.TryGetScrollBoxAround(button, out var buttonList) && buttonList == list;

        if (inList && IsAlongScrollAxis(list, along) && TryGetNearest(buttons, current, from, along, InSameList, out next))
        {
            return true;
        }

        return TryGetNearest(buttons, current, from, along, button => InSameList(button) || IsInView(button), out next);
    }

    private bool IsAlongScrollAxis(Id<GuiNode> scrollBox, Vector2 along) =>
        guiLayoutService.GetLayoutAxis(scrollBox) == UIAxis.Y
            ? MathF.Abs(along.Y) > MathF.Abs(along.X)
            : MathF.Abs(along.X) > MathF.Abs(along.Y);

    private bool IsInView(Id<GuiNode> button)
    {
        if (!guiLayoutService.TryGetClip(button, out var clip))
        {
            return true;
        }

        if (!guiLayoutService.TryGetBoxPosition(button, out var box))
        {
            return false;
        }

        return Overlaps(box.Position.X.Value, box.Size.X.Value, clip.Position.X.Value, clip.Size.X.Value)
            && Overlaps(box.Position.Y.Value, box.Size.Y.Value, clip.Position.Y.Value, clip.Size.Y.Value);
    }

    private static bool Overlaps(float start, float length, float otherStart, float otherLength) =>
        start < otherStart + otherLength && otherStart < start + length;

    private bool TryGetNearest(List<Id<GuiNode>> buttons, Id<GuiNode> current, Vector2 from, Vector2 along,
        Func<Id<GuiNode>, bool> include, out Id<GuiNode> next)
    {
        next = default;
        var bestScore = float.MaxValue;
        foreach (var button in buttons)
        {
            if (button == current || !include(button) || !TryGetCentre(button, out var to))
            {
                continue;
            }

            var offset = to - from;
            var forward = Vector2.Dot(offset, along);
            if (forward <= 0.5f)
            {
                continue;
            }

            var sideways = MathF.Abs(offset.X * along.Y - offset.Y * along.X);
            var score = forward + OffAxisWeight * sideways;
            if (score < bestScore)
            {
                bestScore = score;
                next = button;
            }
        }

        return bestScore < float.MaxValue;
    }

    /// <summary>Activates the selected button, as a click on it would; with none selected, selects the first.</summary>
    public void Activate()
    {
        if (guiFocusService.FocusedNode is { } focused && GetButtons().Contains(focused))
        {
            guiActivationService.Activate(focused);
        }
        else
        {
            FocusFirst();
        }
    }

    /// <summary>The scope's buttons in layout order (depth first).</summary>
    private List<Id<GuiNode>> GetButtons()
    {
        List<Id<GuiNode>> buttons = [];
        if (CurrentScope is { } scope)
        {
            CollectButtons(scope, buttons);
        }

        return buttons;
    }

    /// <returns>Whether the node or one of its descendants is interactive.</returns>
    private bool CollectButtons(Id<GuiNode> node, List<Id<GuiNode>> buttons)
    {
        var anyInteractiveBelow = false;
        var insertAt = buttons.Count;
        if (guiNodeService.TryGetChildren(node, out var children))
        {
            foreach (var child in children)
            {
                anyInteractiveBelow |= CollectButtons(child, buttons);
            }
        }

        var interactive = guiElementService.TryGetElement(node, out var element) && element.Interactive;
        if (interactive && !anyInteractiveBelow && IsEnabledAndShown(node))
        {
            buttons.Insert(insertAt, node);
        }

        return interactive || anyInteractiveBelow;
    }

    private bool IsEnabledAndShown(Id<GuiNode> node) =>
        stateService.TryGetState(node, out var state) && state.HasFlag(GuiNodeState.Enabled) && state.HasFlag(GuiNodeState.Show);

    private bool TryGetCentre(Id<GuiNode> node, out Vector2 centre)
    {
        // A node mounted this frame has a box before layout gives it a size
        if (!guiLayoutService.TryGetBoxPosition(node, out var box) || box.Size.X.Value <= 0 || box.Size.Y.Value <= 0)
        {
            centre = default;
            return false;
        }

        centre = new Vector2(box.Position.X.Value + box.Size.X.Value / 2f, box.Position.Y.Value + box.Size.Y.Value / 2f);
        return true;
    }
}
