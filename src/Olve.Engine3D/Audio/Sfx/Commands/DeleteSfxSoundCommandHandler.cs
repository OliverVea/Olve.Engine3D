using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class DeleteSfxSoundCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument SoundArgument = new("sound", "The sound's id or name", true);

    public string Verb => "delete-sfx-sound";
    public string HelpString => "Deletes a sound from the SFX library; the slots bound to it become unbound";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [SoundArgument, SfxArguments.DryRun];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Sound(library, context, SoundArgument).TryPickProblems(out var problems, out var sound)
            || library.PlanSoundDeletion(sound.Id).TryPickProblems(out problems, out var deletion))
        {
            return problems;
        }

        return SfxDeletionOutput.Run(library, context, deletion);
    }
}
