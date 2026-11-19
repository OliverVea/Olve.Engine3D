using Olve.Utilities.Ids;

namespace Olve.Engine3D.Input.Actions;

public static class InputBindingExtensions
{
    /// <summary>
    /// Applies the user's bindings to the actions: an action named in <paramref name="overrides"/> takes those
    /// bindings, the others keep their defaults. The host checks the overrides' action names at launch
    /// (<c>InputBindingConfiguration</c>); overrides for no registered action of that kind have no effect.
    /// </summary>
    public static InputBindings Resolve(this InputBindingOverrides overrides, IEnumerable<InputAction> actions)
    {
        var registered = actions.ToArray();
        var buttonOverrides = overrides.ButtonBindings.ToDictionary(StringComparer.OrdinalIgnoreCase);
        var directionalOverrides = overrides.DirectionalBindings.ToDictionary(StringComparer.OrdinalIgnoreCase);

        var buttonBindings = new Dictionary<Id<InputAction>, IReadOnlyList<ButtonBinding>>();
        foreach (var action in registered.OfType<ButtonAction>())
        {
            buttonBindings[action.Id] = buttonOverrides.GetValueOrDefault(action.Name) ?? action.DefaultBindings;
        }

        var directionalBindings = new Dictionary<Id<InputAction>, IReadOnlyList<DirectionalBinding>>();
        foreach (var action in registered.OfType<DirectionalAction>())
        {
            directionalBindings[action.Id] =
                directionalOverrides.GetValueOrDefault(action.Name) ?? action.DefaultBindings;
        }

        return new InputBindings
        {
            Actions = registered,
            ButtonBindings = buttonBindings,
            DirectionalBindings = directionalBindings,
        };
    }
}
