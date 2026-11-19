using System.Text.Json;
using System.Text.Json.Serialization;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public static class SfxArguments
{
    public static readonly CommandArgument DryRun = new("dry-run", "true to only report what would change (default false)");

    public static Result<PatchPanel> Panel(SfxLibraryManager library, CommandContext context, CommandArgument argument) =>
        Find(library, context, argument, "panel", library.Content.Panels, panel => panel.Id.Value, panel => panel.Name);

    public static Result<LibrarySound> Sound(SfxLibraryManager library, CommandContext context, CommandArgument argument) =>
        Find(library, context, argument, "sound", library.Content.Sounds, sound => sound.Id.Value, sound => sound.Name);

    public static Result<SfxSlot> Slot(SfxLibraryManager library, CommandContext context, CommandArgument argument) =>
        Find(library, context, argument, "SFX slot", library.Slots, slot => slot.Id.Value, slot => slot.Name);

    public static Result<bool> Flag(CommandContext context, CommandArgument argument) => context.GetFlag(argument);

    public static Result<float?> Number(CommandContext context, CommandArgument argument) =>
        MixerArguments.ParseFloat(context, argument);

    public static string Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Serialize(value, typeInfo);

    private static Result<T> Find<T>(
        SfxLibraryManager library, CommandContext context, CommandArgument argument, string kind, IEnumerable<T> items, Func<T, Id> id, Func<T, string> name)
    {
        if (library.CheckLoaded().TryPickProblems(out var problems)
            || context.GetRequiredArgument(argument).TryPickProblems(out problems, out var text))
        {
            return problems;
        }

        var list = items.ToList();
        if (Id.TryParse(text, out var parsed))
        {
            return list.FirstOrDefault(item => id(item) == parsed) is { } byId
                ? byId
                : new ResultProblem("There's no {0} with the id {1}", kind, text);
        }

        var named = list.Where(item => name(item) == text).ToList();
        return named.Count switch
        {
            1 => named[0],
            0 => new ResultProblem("There's no {0} named '{1}'", kind, text),
            _ => new ResultProblem("{0} {1}s are named '{2}'; use its id", named.Count, kind, text),
        };
    }
}
