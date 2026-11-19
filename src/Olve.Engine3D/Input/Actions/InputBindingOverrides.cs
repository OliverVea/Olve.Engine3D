namespace Olve.Engine3D.Input.Actions;

/// <summary>
/// The user's bindings, by action name. An action listed here uses these bindings instead of its defaults; an empty
/// list leaves it unbound.
/// </summary>
public sealed record InputBindingOverrides
{
    public static InputBindingOverrides None { get; } = new();

    public IReadOnlyDictionary<string, IReadOnlyList<ButtonBinding>> ButtonBindings { get; init; } =
        new Dictionary<string, IReadOnlyList<ButtonBinding>>();

    public IReadOnlyDictionary<string, IReadOnlyList<DirectionalBinding>> DirectionalBindings { get; init; } =
        new Dictionary<string, IReadOnlyList<DirectionalBinding>>();
}
