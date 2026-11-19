namespace Olve.Engine3D.Input.Actions;

/// <summary>
/// The actions engine services ask about. Games register them along with their own.
/// </summary>
public static class EngineInputActions
{
    /// <summary>Dumps the framebuffers to <c>screenshots/</c>.</summary>
    public static ButtonAction Screenshot { get; } = new("Screenshot", [Key.F12]);

    // Moving the GUI selection and pressing the selected button, while GuiNavigationService navigates a tree
    public static ButtonAction GuiUp { get; } = new("GuiUp", [Key.Up, Key.W]);
    public static ButtonAction GuiDown { get; } = new("GuiDown", [Key.Down, Key.S]);
    public static ButtonAction GuiLeft { get; } = new("GuiLeft", [Key.Left, Key.A]);
    public static ButtonAction GuiRight { get; } = new("GuiRight", [Key.Right, Key.D]);
    public static ButtonAction GuiActivate { get; } = new("GuiActivate", [Key.Space, Key.Enter]);

    public static IReadOnlyList<ButtonAction> ButtonActions { get; } = [Screenshot, GuiUp, GuiDown, GuiLeft, GuiRight, GuiActivate];
}
