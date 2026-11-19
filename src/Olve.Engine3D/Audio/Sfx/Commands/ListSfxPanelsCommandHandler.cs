using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class ListSfxPanelsCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    public string Verb => "list-sfx-panels";
    public string HelpString => "The SFX library's patch panels: id, name, inputs and how many sounds they make";
    public IReadOnlyList<CommandArgument> Arguments => [];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (library.CheckLoaded().TryPickProblems(out var problems))
        {
            return problems;
        }

        var panels = library.Content.Panels
            .Select(panel => new SfxPanelSummaryJson(
                panel.Id.Value.Value,
                panel.Name,
                [.. panel.Patch.Parameters.Select(input => new SfxInputJson(input.Id.Value.Value, input.Name))],
                library.Content.Sounds.Count(sound => sound.Panel == panel.Id)))
            .OrderBy(panel => panel.Name, StringComparer.Ordinal)
            .ToList();
        return new CommandOutput(SfxArguments.Json(panels, SfxCommandJsonContext.Default.ListSfxPanelSummaryJson));
    }
}
