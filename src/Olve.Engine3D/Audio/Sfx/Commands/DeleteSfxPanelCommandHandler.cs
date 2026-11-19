using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class DeleteSfxPanelCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument PanelArgument = new("panel", "The panel's id or name", true);

    public string Verb => "delete-sfx-panel";
    public string HelpString => "Deletes a patch panel and every sound made from it; the slots bound to those become unbound";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [PanelArgument, SfxArguments.DryRun];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Panel(library, context, PanelArgument).TryPickProblems(out var problems, out var panel)
            || library.PlanPanelDeletion(panel.Id).TryPickProblems(out problems, out var deletion))
        {
            return problems;
        }

        return SfxDeletionOutput.Run(library, context, deletion);
    }
}
