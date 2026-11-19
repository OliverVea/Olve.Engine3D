using System.Text.Json;
using Olve.Engine3D.Input.Actions;

namespace Olve.Engine3D.Commands;

public class ListActionsCommandHandler(InputActionManager inputActionManager) : ICommandHandler
{
    public string Verb => "list-actions";
    public string HelpString => "Lists the input actions with their kind and current bindings";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        var actions = inputActionManager.Actions
            .Select(action => new ActionList.Action(action.Name, GetKind(action), GetBindingTexts(action)))
            .ToArray();

        var json = JsonSerializer.Serialize(new ActionList(actions), EngineJsonContext.Default.ActionList);
        return new CommandOutput(json);
    }

    private static string GetKind(InputAction action) => action switch
    {
        ButtonAction => "button",
        DirectionalAction => "directional",
        _ => action.GetType().Name,
    };

    private string[] GetBindingTexts(InputAction action) => action switch
    {
        ButtonAction button => inputActionManager.GetBindings(button).Select(b => b.ToString()).ToArray(),
        DirectionalAction directional => inputActionManager.GetBindings(directional).Select(b => b.ToString()).ToArray(),
        _ => [],
    };
}

internal sealed record ActionList(ActionList.Action[] Actions)
{
    internal sealed record Action(string Name, string Kind, string[] Bindings);
}
