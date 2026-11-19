using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using Olve.Engine3D.Input;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Scenes;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// Plays notes from <c>main.js</c>'s input handlers as each event arrives, so a note sounds at once rather than at the
/// next frame, which a phone may draw tens of milliseconds later. The page still reports every event to the host as
/// well; the rack then leaves the notes played here alone (see <see cref="RackInputService"/>).
/// </summary>
public static partial class SynthInput
{
    private static RackInputService? _rack;

    public static void Attach(RackInputService rack) => _rack = rack;

    /// <summary>A key went down or up; <paramref name="code"/> is the DOM's <c>KeyboardEvent.code</c>.</summary>
    [JSExport]
    public static void Key(string code, bool isDown)
    {
        if (ToKey(code) is { } key)
        {
            _rack?.KeyArrived(key, isDown);
        }
    }

    /// <summary>A finger touched (by its pointer id), or the mouse's main button went down, at canvas pixels.</summary>
    [JSExport]
    public static void Press(bool isTouch, int pointerId, double x, double y) =>
        _rack?.PressArrived(isTouch ? Pointer.Touch(pointerId) : Pointer.Mouse, new Vector2((float)x, (float)y));

    [JSExport]
    public static void Release(bool isTouch, int pointerId) => _rack?.ReleaseArrived(isTouch ? Pointer.Touch(pointerId) : Pointer.Mouse);

    // The keys that play notes, as the DOM names them
    private static Key? ToKey(string code) => code switch
    {
        "Semicolon" => Input.Key.Semicolon,
        "Quote" => Input.Key.Apostrophe,
        ['K', 'e', 'y', var letter] when Enum.TryParse<Key>(letter.ToString(), out var key) => key,
        _ => null,
    };
}

/// <summary>Hands the scene's rack input to <see cref="SynthInput"/>.</summary>
public sealed class SynthInputService(RackInputService rack) : ISceneService
{
    public Result Load()
    {
        SynthInput.Attach(rack);
        return Result.Success();
    }
}
