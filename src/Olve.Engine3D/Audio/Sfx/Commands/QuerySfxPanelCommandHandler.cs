using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class QuerySfxPanelCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument PanelArgument = new("panel", "The panel's id or name", true);

    public string Verb => "query-sfx-panel";
    public string HelpString => "A patch panel in full: its nodes, cables, inputs and the sounds made from it";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [PanelArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Panel(library, context, PanelArgument).TryPickProblems(out var problems, out var panel))
        {
            return problems;
        }

        var sounds = library.Content.Sounds.Where(sound => sound.Panel == panel.Id).Select(sound => sound.Id.Value.Value).Order().ToList();
        return new CommandOutput(SfxArguments.Json(new SfxPanelJson(SfxLibraryCodec.ToDocument(panel), sounds), SfxCommandJsonContext.Default.SfxPanelJson));
    }
}
