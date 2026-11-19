using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class BindSfxSlotCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument SlotArgument = new("slot", "The slot's name or id", true);
    private static readonly CommandArgument SoundArgument = new("sound", "The sound's id or name", true);
    private static readonly CommandArgument PitchArgument = new("pitch-variation", "Semitones each play may go up or down (default: the slot's, or as bound before)");
    private static readonly CommandArgument GainArgument = new("gain-variation", "Decibels each play may go up or down (default: the slot's, or as bound before)");

    public string Verb => "bind-sfx-slot";
    public string HelpString => "Binds an SFX slot to a sound in the library, optionally with its own pitch and gain variation";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [SlotArgument, SoundArgument, PitchArgument, GainArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Slot(library, context, SlotArgument).TryPickProblems(out var problems, out var slot)
            || SfxArguments.Sound(library, context, SoundArgument).TryPickProblems(out problems, out var sound)
            || SfxArguments.Number(context, PitchArgument).TryPickProblems(out problems, out var pitch)
            || SfxArguments.Number(context, GainArgument).TryPickProblems(out problems, out var gain))
        {
            return problems;
        }

        if (library.Bind(slot.Id, sound.Id, pitch, gain).TryPickProblems(out problems))
        {
            return problems;
        }

        return new CommandOutput($"Bound SFX slot '{slot.Name}' to sound '{sound.Name}'");
    }
}
