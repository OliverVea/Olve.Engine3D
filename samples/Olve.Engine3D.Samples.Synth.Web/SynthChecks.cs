using System.Runtime.InteropServices.JavaScript;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Space;
using Olve.Engine3D.Scenes;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// What <c>scripts/check-web.py --synth</c> reaches through the page (<c>window.synthChecks</c>): it patches cables the way
/// a drag would and reads the lights as the room draws them, so a check sees what C# does with the audio's readback.
/// </summary>
public static partial class SynthChecks
{
    private static SynthRack? _rack;
    private static RackLightsService? _lights;
    private static ScopeService? _scopes;
    private static OutputLevelsService? _levels;

    public static void Attach(SynthRack rack, RackLightsService lights, ScopeService scopes, OutputLevelsService levels)
    {
        _rack = rack;
        _lights = lights;
        _scopes = scopes;
        _levels = levels;
    }

    /// <summary>The patch's size: how many modules and how many cables it has.</summary>
    [JSExport]
    public static int[] PatchSize() => _rack is null ? [] : [_rack.Patch.Modules.Count, _rack.Patch.Cables.Count];

    /// <summary>
    /// Builds a module of a kind straight in the audio, outside the patch, under an id the patch doesn't use, with its knobs
    /// as their definitions start them: for a check of a module the preset lacks. Empty if it was built, else why not.
    /// </summary>
    [JSExport]
    public static string AddToAudio(string kind, int id)
    {
        if (_rack is null || !Enum.TryParse<ModuleKind>(kind, out var moduleKind))
        {
            return _rack is null ? "The rack isn't loaded" : $"No module kind {kind}";
        }

        var module = new ModuleId(id);
        _rack.Audio.AddModule(module, moduleKind, voices: 1);
        foreach (var parameter in Modules.Of(moduleKind).Parameters)
        {
            _rack.Audio.SetParameter(module, parameter.Name, parameter.Default);
        }

        return "";
    }

    /// <summary>Sets a knob of a module <see cref="AddToAudio"/> built, in the audio.</summary>
    [JSExport]
    public static void SetInAudio(int id, string parameter, double value) => _rack?.Audio.SetParameter(new ModuleId(id), parameter, (float)value);

    /// <summary>How many lights an output's level ring lights now, the module found by its title as <see cref="Plug"/> finds it; -1 if there's no such module.</summary>
    [JSExport]
    public static double Lit(string title, string jack) =>
        _rack is not null && _levels is not null && Find(_rack.Patch, title) is { } module ? _levels.Lit(new JackRef(module.Id, jack)) : -1;

    /// <summary>
    /// Patches a cable between two modules, each by its title, numbered from 1 as the panels number them when the rack has
    /// more than one ("SCOPE 2"); empty if it was patched, else why not.
    /// </summary>
    [JSExport]
    public static string Plug(string fromTitle, string fromJack, string toTitle, string toJack)
    {
        if (_rack is null)
        {
            return "The rack isn't loaded";
        }

        var from = Find(_rack.Patch, fromTitle);
        var to = Find(_rack.Patch, toTitle);
        if (from is null || to is null)
        {
            return $"No module {(from is null ? fromTitle : toTitle)}";
        }

        return _rack.Connect(new JackRef(from.Id, fromJack), new JackRef(to.Id, toJack)).TryPickProblems(out var problems)
            ? string.Join("; ", problems.Select(p => p.ToBriefString()))
            : "";
    }

    /// <summary>Unplugs the cable between two modules' jacks, found as <see cref="Plug"/> finds them; empty if it was unplugged, else why not.</summary>
    [JSExport]
    public static string Unplug(string fromTitle, string fromJack, string toTitle, string toJack)
    {
        if (_rack is null)
        {
            return "The rack isn't loaded";
        }

        var from = Find(_rack.Patch, fromTitle);
        var to = Find(_rack.Patch, toTitle);
        if (from is null || to is null)
        {
            return $"No module {(from is null ? fromTitle : toTitle)}";
        }

        return _rack.Disconnect(new Cable(new JackRef(from.Id, fromJack), new JackRef(to.Id, toJack))).TryPickProblems(out var problems)
            ? string.Join("; ", problems.Select(p => p.ToBriefString()))
            : "";
    }

    /// <summary>Sets a knob of a module, found by its title as <see cref="Plug"/> finds it; empty if it was set, else why not.</summary>
    [JSExport]
    public static string Set(string title, string parameter, double value)
    {
        if (_rack is null)
        {
            return "The rack isn't loaded";
        }

        return Find(_rack.Patch, title) is not { } module
            ? $"No module {title}"
            : _rack.SetValue(module.Id, parameter, (float)value).TryPickProblems(out var problems)
                ? string.Join("; ", problems.Select(p => p.ToBriefString()))
                : "";
    }

    /// <summary>A knob's value in the patch, the module found by its title as <see cref="Plug"/> finds it; NaN if there's no such knob.</summary>
    [JSExport]
    public static double Get(string title, string parameter) =>
        _rack is not null && Find(_rack.Patch, title) is { } module && module.Definition.FindParameter(parameter) is not null
            ? _rack.Patch.GetValue(module.Id, parameter)
            : double.NaN;

    private static Module? Find(SynthPatch patch, string title)
    {
        var parts = title.Split(' ');
        var numbered = parts.Length > 1 && int.TryParse(parts[^1], out var number) ? (string.Join(' ', parts[..^1]), number) : (title, 1);
        return patch.Modules.Where(m => m.Definition.Title == numbered.Item1).Skip(numbered.Item2 - 1).FirstOrDefault();
    }

    /// <summary>The scopes' screens now: for each, whether its trace is drawn and its tallest swing from the middle.</summary>
    [JSExport]
    public static double[] Scopes() => _scopes is null ? [] : [.. _scopes.Screens.SelectMany(s => new double[] { s.Live ? 1 : 0, s.Peak })];

    /// <summary>The lights as the room draws them now: the left's intensity, hue and saturation, then the right's.</summary>
    [JSExport]
    public static double[] Lights()
    {
        if (_lights is null)
        {
            return [];
        }

        var (left, right) = (_lights.Lights.Left, _lights.Lights.Right);
        return [left.Intensity, left.Hue, left.Saturation, right.Intensity, right.Hue, right.Saturation];
    }
}

/// <summary>Hands the scene's rack, lights and scopes to <see cref="SynthChecks"/>.</summary>
public sealed class SynthChecksService(SynthRack rack, RackLightsService lights, ScopeService scopes, OutputLevelsService levels) : ISceneService
{
    public Result Load()
    {
        SynthChecks.Attach(rack, lights, scopes, levels);
        return Result.Success();
    }
}
