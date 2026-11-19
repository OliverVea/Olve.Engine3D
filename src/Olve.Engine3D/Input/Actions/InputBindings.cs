using Olve.Utilities.Ids;

namespace Olve.Engine3D.Input.Actions;

/// <summary>
/// The registered actions and the bindings in effect for each: the user's from <see cref="InputBindingOverrides"/>,
/// or the action's defaults. Built at startup by <see cref="InputBindingExtensions.Resolve"/>.
/// </summary>
public sealed record InputBindings
{
    /// <summary>In registration order.</summary>
    public IReadOnlyList<InputAction> Actions { get; init; } = [];

    public IReadOnlyDictionary<Id<InputAction>, IReadOnlyList<ButtonBinding>> ButtonBindings { get; init; } =
        new Dictionary<Id<InputAction>, IReadOnlyList<ButtonBinding>>();

    public IReadOnlyDictionary<Id<InputAction>, IReadOnlyList<DirectionalBinding>> DirectionalBindings { get; init; } =
        new Dictionary<Id<InputAction>, IReadOnlyList<DirectionalBinding>>();
}
