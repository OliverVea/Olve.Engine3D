using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Audio.Sfx.Commands;

internal static class SfxDeletionOutput
{
    public static Result<CommandOutput> Run(SfxLibraryManager library, CommandContext context, SfxDeletion deletion)
    {
        if (SfxArguments.Flag(context, SfxArguments.DryRun).TryPickProblems(out var problems, out var dryRun))
        {
            return problems;
        }

        var slots = deletion.UnboundSlots.Select(id => library.Slot(id)?.Name ?? id.ToString()).Order(StringComparer.Ordinal).ToList();
        if (!dryRun && library.Delete(deletion).TryPickProblems(out problems))
        {
            return problems;
        }

        var json = new SfxDeletionJson(
            dryRun,
            [.. deletion.Panels.Select(panel => panel.Name)],
            [.. deletion.Sounds.Select(sound => sound.Name).Order(StringComparer.Ordinal)],
            slots);
        return new CommandOutput(SfxArguments.Json(json, SfxCommandJsonContext.Default.SfxDeletionJson));
    }
}
