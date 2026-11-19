namespace Olve.Engine3D.Input.Actions;

/// <summary>
/// Binds a <see cref="ButtonAction"/> to a trigger button, optionally held together with modifiers, from any devices
/// (e.g. Shift+R: modifier Shift, trigger R). A <see cref="Button"/>, <see cref="Key"/> or <see cref="MouseButton"/>
/// converts to a binding without modifiers.
/// </summary>
/// <remarks>Two bindings are equal when they have the same trigger and modifiers, in any order.</remarks>
public readonly record struct ButtonBinding
{
    private readonly Button[]? _modifiers;

    /// <param name="buttons">The modifiers, then the trigger last.</param>
    public ButtonBinding(params IEnumerable<Button> buttons)
    {
        var all = buttons.ToArray();
        if (all.Length == 0)
        {
            throw new ArgumentException("A binding needs a trigger button.", nameof(buttons));
        }

        Trigger = all[^1];
        _modifiers = all[..^1].Distinct().ToArray();
        if (_modifiers.Contains(Trigger))
        {
            throw new ArgumentException($"{Trigger} can't be both the trigger and a modifier.", nameof(buttons));
        }
    }

    /// <summary>The button whose press triggers the binding.</summary>
    public Button Trigger { get; }

    /// <summary>The buttons that must be held while the trigger is pressed, in the order they were given.</summary>
    public IReadOnlyList<Button> Modifiers => _modifiers ?? [];

    /// <summary>The modifiers, then the trigger.</summary>
    public IEnumerable<Button> Buttons => [.. Modifiers, Trigger];

    public static implicit operator ButtonBinding(Button button) => new(button);

    public static implicit operator ButtonBinding(Key key) => new(Button.Of(key));

    public static implicit operator ButtonBinding(MouseButton mouseButton) => new(Button.Of(mouseButton));

    public bool Equals(ButtonBinding other) =>
        Trigger == other.Trigger && Modifiers.Count == other.Modifiers.Count && Modifiers.All(other.Modifiers.Contains);

    public override int GetHashCode() =>
        Modifiers.Aggregate(Trigger.GetHashCode(), (hash, modifier) => hash ^ modifier.GetHashCode());

    /// <summary>The binding in the text <c>Input:Bindings</c> takes, e.g. <c>Key.R</c> or <c>Key.ShiftLeft+Key.R</c>.</summary>
    public override string ToString() => string.Join("+", Buttons);
}

/// <summary>
/// Binds a <see cref="DirectionalAction"/> to four buttons, one per direction.
/// </summary>
public readonly record struct DirectionalBinding(Button Up, Button Down, Button Left, Button Right)
{
    /// <summary>The binding in the text <c>Input:Bindings</c> takes: up, down, left, right.</summary>
    public override string ToString() => $"{Up},{Down},{Left},{Right}";
}
