using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class ListSfxSoundsCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    public string Verb => "list-sfx-sounds";
    public string HelpString => "The SFX library's sounds: id, name, panel, length and the slots bound to them";
    public IReadOnlyList<CommandArgument> Arguments => [];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (library.CheckLoaded().TryPickProblems(out var problems))
        {
            return problems;
        }

        var sounds = library.Content.Sounds
            .Select(sound => new SfxSoundSummaryJson(
                sound.Id.Value.Value,
                sound.Name,
                sound.Panel.Value.Value,
                library.Panel(sound.Panel)?.Name ?? "",
                sound.Length,
                [.. library.SlotsBoundTo(sound.Id).Select(slot => slot.Name).Order(StringComparer.Ordinal)]))
            .OrderBy(sound => sound.Name, StringComparer.Ordinal)
            .ToList();
        return new CommandOutput(SfxArguments.Json(sounds, SfxCommandJsonContext.Default.ListSfxSoundSummaryJson));
    }
}
