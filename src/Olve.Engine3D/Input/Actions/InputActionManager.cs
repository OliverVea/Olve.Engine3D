using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Input.Actions;

/// <summary>
/// Reads the actions' <see cref="InputBindings"/> against the frame's <see cref="InputState"/>. Services ask whether
/// an action triggered instead of checking a key, so the player's bindings apply everywhere.
/// </summary>
/// <remarks>
/// A button binding is a trigger button, optionally with modifiers that must be held. Of the bindings with the same
/// trigger whose modifiers are all held, the one with the most modifiers wins, across all actions: with R bound to one
/// action and Shift+R to another, Shift+R triggers only the second.
/// Consuming an action consumes the buttons bound to it (<see cref="InputState.Consume"/>), so the services after it
/// see neither the action nor anything else bound to those buttons; consumed buttons read as not pressed here too.
/// Like held keys, directional actions read for continuous movement aren't consumed. An action that isn't
/// registered has no bindings.
/// </remarks>
public sealed class InputActionManager(
    InputState inputState,
    InputBindings bindings,
    ILogger<InputActionManager> logger)
{
    private readonly ButtonBinding[] _allButtonBindings = bindings.ButtonBindings.Values.SelectMany(b => b).ToArray();

    /// <summary>The registered actions, in registration order.</summary>
    public IReadOnlyList<InputAction> Actions => bindings.Actions;

    public IReadOnlyList<ButtonBinding> GetBindings(ButtonAction action) =>
        bindings.ButtonBindings.GetValueOrDefault(action.Id) ?? [];

    public IReadOnlyList<DirectionalBinding> GetBindings(DirectionalAction action) =>
        bindings.DirectionalBindings.GetValueOrDefault(action.Id) ?? [];

    /// <summary>
    /// Whether one of the action's bindings was triggered during the frame, and no binding with the same trigger and more
    /// modifiers was triggered too.
    /// </summary>
    public bool WasTriggered(ButtonAction action) =>
        GetBindings(action).Any(binding => IsTriggered(binding) && !IsOutranked(binding, IsTriggered));

    /// <summary>
    /// Whether one of the action's bindings is held at the end of the frame's events, and no binding with the same trigger
    /// and more modifiers is held too.
    /// </summary>
    public bool IsDown(ButtonAction action) =>
        GetBindings(action).Any(binding => IsHeld(binding) && !IsOutranked(binding, IsHeld));

    /// <summary>
    /// Consumes the action if it triggered during the frame and nothing has consumed it yet.
    /// </summary>
    /// <returns>Whether the action triggered, so the caller should handle it.</returns>
    public bool TryConsume(ButtonAction action)
    {
        if (!WasTriggered(action))
        {
            return false;
        }

        Consume(action);
        logger.LogDebug("Input action {Action} triggered", action);
        return true;
    }

    /// <summary>Consumes the buttons bound to the action until the frame ends.</summary>
    public void Consume(ButtonAction action)
    {
        foreach (var button in GetBindings(action).SelectMany(binding => binding.Buttons))
        {
            inputState.Consume(button);
        }
    }

    /// <summary>
    /// A binding is triggered when its trigger was pressed during the frame while its modifiers are held (or were pressed
    /// too), so a press and a release in the same frame still count.
    /// </summary>
    private bool IsTriggered(ButtonBinding binding) =>
        inputState.WasPressed(binding.Trigger)
        && binding.Modifiers.All(modifier => inputState.IsDown(modifier) || inputState.WasPressed(modifier));

    private bool IsHeld(ButtonBinding binding) => binding.Buttons.All(inputState.IsDown);

    /// <summary>Whether a binding of any action with the same trigger and more modifiers also applies, and so wins over this one.</summary>
    private bool IsOutranked(ButtonBinding binding, Func<ButtonBinding, bool> applies) =>
        _allButtonBindings.Any(other =>
            other.Trigger == binding.Trigger && other.Modifiers.Count > binding.Modifiers.Count && applies(other));

    /// <summary>
    /// The direction the action's held buttons point, summed over its bindings, e.g. (1, 1) for up and right.
    /// </summary>
    public Vector2 GetValue(DirectionalAction action)
    {
        var value = Vector2.Zero;

        foreach (var binding in GetBindings(action))
        {
            value += new Vector2(
                GetHeldValue(binding.Right) - GetHeldValue(binding.Left),
                GetHeldValue(binding.Up) - GetHeldValue(binding.Down));
        }

        return value;
    }

    private float GetHeldValue(Button button) => inputState.IsDown(button) ? 1f : 0f;
}
