using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class UnbindSfxSlotCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument SlotArgument = new("slot", "The slot's name or id", true);

    public string Verb => "unbind-sfx-slot";
    public string HelpString => "Unbinds an SFX slot: it plays its default sound, with its default variation";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [SlotArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Slot(library, context, SlotArgument).TryPickProblems(out var problems, out var slot)
            || library.Unbind(slot.Id).TryPickProblems(out problems))
        {
            return problems;
        }

        return new CommandOutput($"Unbound SFX slot '{slot.Name}'");
    }
}
