using Olve.Engine3D.Input;

namespace Olve.Engine3D.Platform.Web;

internal static class DomInputMapping
{
    private static readonly Dictionary<string, Key> Keys = CreateKeys();

    public static Key ToEngineKey(string code) => Keys.GetValueOrDefault(code, Key.Unknown);

    public static MouseButton? ToEngineMouseButton(int button) => button switch
    {
        0 => MouseButton.Left,
        1 => MouseButton.Middle,
        2 => MouseButton.Right,
        _ => null,
    };

    private static Dictionary<string, Key> CreateKeys()
    {
        var keys = new Dictionary<string, Key>
        {
            ["ArrowUp"] = Key.Up,
            ["ArrowDown"] = Key.Down,
            ["ArrowLeft"] = Key.Left,
            ["ArrowRight"] = Key.Right,
            ["ShiftLeft"] = Key.ShiftLeft,
            ["ShiftRight"] = Key.ShiftRight,
            ["ControlLeft"] = Key.ControlLeft,
            ["ControlRight"] = Key.ControlRight,
            ["AltLeft"] = Key.AltLeft,
            ["AltRight"] = Key.AltRight,
            ["Space"] = Key.Space,
            ["Enter"] = Key.Enter,
            ["Escape"] = Key.Escape,
            ["Backspace"] = Key.Backspace,
            ["Tab"] = Key.Tab,
            ["Delete"] = Key.Delete,
            ["Insert"] = Key.Insert,
            ["Home"] = Key.Home,
            ["End"] = Key.End,
            ["PageUp"] = Key.PageUp,
            ["PageDown"] = Key.PageDown,
            ["Minus"] = Key.Minus,
            ["Equal"] = Key.Equal,
            ["Period"] = Key.Period,
            ["Comma"] = Key.Comma,
            ["Slash"] = Key.Slash,
            ["Semicolon"] = Key.Semicolon,
            ["Quote"] = Key.Apostrophe,
            ["BracketLeft"] = Key.LeftBracket,
            ["BracketRight"] = Key.RightBracket,
            ["Backslash"] = Key.BackSlash,
            ["Backquote"] = Key.GraveAccent,
        };

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            keys[$"Key{letter}"] = Enum.Parse<Key>(letter.ToString());
        }

        for (var digit = 0; digit <= 9; digit++)
        {
            keys[$"Digit{digit}"] = Enum.Parse<Key>($"Number{digit}");
        }

        for (var function = 1; function <= 12; function++)
        {
            keys[$"F{function}"] = Enum.Parse<Key>($"F{function}");
        }

        return keys;
    }
}
