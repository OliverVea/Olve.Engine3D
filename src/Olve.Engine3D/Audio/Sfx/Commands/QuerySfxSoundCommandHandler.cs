using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class QuerySfxSoundCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument SoundArgument = new("sound", "The sound's id or name", true);

    public string Verb => "query-sfx-sound";
    public string HelpString => "A sound in full: its panel, length, input values and the slots bound to it";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [SoundArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Sound(library, context, SoundArgument).TryPickProblems(out var problems, out var sound))
        {
            return problems;
        }

        var json = new SfxSoundJson(
            SfxLibraryCodec.ToDocument(sound),
            library.Panel(sound.Panel)?.Name ?? "",
            [.. library.SlotsBoundTo(sound.Id).Select(slot => slot.Name).Order(StringComparer.Ordinal)]);
        return new CommandOutput(SfxArguments.Json(json, SfxCommandJsonContext.Default.SfxSoundJson));
    }
}
