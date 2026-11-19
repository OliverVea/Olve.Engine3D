using Olve.Utilities.Ids;

namespace Olve.Engine3D.Input.Actions;

/// <summary>
/// Something the player does, such as rotating a tool or panning the camera, that services ask about instead of a
/// key. Actions are defined once, registered with <see cref="InputActionServiceExtensions.AddInputActions"/>, and
/// bound to inputs by their default bindings or the user's.
/// </summary>
public abstract class InputAction(string name)
{
    /// <summary>Derived from <see cref="Name"/>, so actions with the same name share it.</summary>
    public Id<InputAction> Id { get; } = Olve.Utilities.Ids.Id.FromName<InputAction>(name);

    /// <summary>Names the action in settings and command output.</summary>
    public string Name { get; } = name;

    public override string ToString() => Name;
}

/// <summary>An action that is triggered and held, like a button.</summary>
public sealed class ButtonAction(string name, IReadOnlyList<ButtonBinding> defaultBindings) : InputAction(name)
{
    public IReadOnlyList<ButtonBinding> DefaultBindings { get; } = defaultBindings;
}

/// <summary>
/// An action with a two-dimensional value, such as camera movement: up is +Y, right is +X.
/// </summary>
public sealed class DirectionalAction(string name, IReadOnlyList<DirectionalBinding> defaultBindings) : InputAction(name)
{
    public IReadOnlyList<DirectionalBinding> DefaultBindings { get; } = defaultBindings;
}
