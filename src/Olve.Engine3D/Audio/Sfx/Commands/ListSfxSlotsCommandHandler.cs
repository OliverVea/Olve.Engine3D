using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class ListSfxSlotsCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    public string Verb => "list-sfx-slots";
    public string HelpString => "The game's SFX slots: the bus each plays on, the sound it's bound to and its pitch and gain variation";
    public IReadOnlyList<CommandArgument> Arguments => [];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (library.CheckLoaded().TryPickProblems(out var problems))
        {
            return problems;
        }

        var slots = library.Slots
            .Select(library.SlotState)
            .Select(state => new SfxSlotJson(
                state.Slot.Id.Value.Value,
                state.Slot.Name,
                state.Slot.Bus.Value,
                state.Sound?.Id.Value.Value,
                state.Sound?.Name,
                new SfxVariationJson(state.Variation.PitchSemitones, state.Variation.GainDb, state.PitchOverridden, state.GainOverridden)))
            .OrderBy(slot => slot.Name, StringComparer.Ordinal)
            .ToList();
        return new CommandOutput(SfxArguments.Json(slots, SfxCommandJsonContext.Default.ListSfxSlotJson));
    }
}
