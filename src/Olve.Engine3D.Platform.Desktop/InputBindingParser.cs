using Olve.Engine3D.Input;
using Olve.Engine3D.Input.Actions;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// Reads bindings from the text that <see cref="Button.ToString"/>, <see cref="ButtonBinding.ToString"/> and
/// <see cref="DirectionalBinding.ToString"/> write, e.g. <c>Key.R</c>, <c>Mouse.Left</c>, a chord such as
/// <c>Key.ShiftLeft+Key.R</c>, or <c>Key.W,Key.S,Key.A,Key.D</c> (up, down, left, right). Names are case-insensitive.
/// </summary>
public static class InputBindingParser
{
    public static Result<Button> ParseButton(string text)
    {
        var parts = text.Trim().Split('.');
        if (parts.Length != 2)
        {
            return new ResultProblem("Invalid button \"{0}\", expected Key.<key> or Mouse.<button>", text);
        }

        var (device, name) = (parts[0], parts[1]);

        if (device.Equals("Key", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseName<Key>(name, out var key) && key != Key.Unknown
                ? Button.Of(key)
                : new ResultProblem("Unknown key \"{0}\" in \"{1}\"", name, text);
        }

        if (device.Equals(nameof(InputDevice.Mouse), StringComparison.OrdinalIgnoreCase))
        {
            return TryParseName<MouseButton>(name, out var mouseButton)
                ? Button.Of(mouseButton)
                : new ResultProblem("Unknown mouse button \"{0}\" in \"{1}\"", name, text);
        }

        return new ResultProblem("Unknown device \"{0}\" in \"{1}\", expected Key or Mouse", device, text);
    }

    /// <summary>Reads a chord: one or more buttons joined by <c>+</c>, e.g. <c>Key.ShiftLeft+Key.R</c>.</summary>
    public static Result<ButtonBinding> ParseButtonBinding(string text)
    {
        var buttons = new List<Button>();
        foreach (var part in text.Split('+'))
        {
            if (ParseButton(part).TryPickProblems(out var problems, out var button))
            {
                return problems.Prepend(new ResultProblem("Invalid binding \"{0}\"", text));
            }

            if (buttons.Contains(button))
            {
                return new ResultProblem("Invalid binding \"{0}\": {1} appears twice", text, button);
            }

            buttons.Add(button);
        }

        return new ButtonBinding(buttons);
    }

    public static Result<DirectionalBinding> ParseDirectionalBinding(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 4)
        {
            return new ResultProblem(
                "Invalid directional binding \"{0}\", expected four buttons: up,down,left,right", text);
        }

        var buttons = new Button[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (ParseButton(parts[i]).TryPickProblems(out var problems, out buttons[i]))
            {
                return problems.Prepend(new ResultProblem("Invalid directional binding \"{0}\"", text));
            }
        }

        return new DirectionalBinding(buttons[0], buttons[1], buttons[2], buttons[3]);
    }

    /// <summary>Parses an enum member by name; numbers aren't names.</summary>
    private static bool TryParseName<TEnum>(string name, out TEnum value) where TEnum : struct, Enum =>
        Enum.TryParse(name, ignoreCase: true, out value) && Enum.IsDefined(value) && !int.TryParse(name, out _);
}
