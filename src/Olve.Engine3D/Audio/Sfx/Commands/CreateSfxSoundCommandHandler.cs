using System.Collections.Immutable;
using System.Globalization;
using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class CreateSfxSoundCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument PanelArgument = new("panel", "The panel's id or name", true);
    private static readonly CommandArgument NameArgument = new("name", "The new sound's name", true);
    private static readonly CommandArgument LengthArgument = new("length", "Seconds the note is held (default 0.3)");
    private static readonly CommandArgument InputsArgument = new("inputs", "Input values by input name or id, e.g. \"Pitch=440,Cutoff=900\"");

    public string Verb => "create-sfx-sound";
    public string HelpString => "Creates a sound in the SFX library from a panel, with values for its inputs";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [PanelArgument, NameArgument, LengthArgument, InputsArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Panel(library, context, PanelArgument).TryPickProblems(out var problems, out var panel)
            || context.GetRequiredArgument(NameArgument).TryPickProblems(out problems, out var name)
            || SfxArguments.Number(context, LengthArgument).TryPickProblems(out problems, out var length)
            || ParseInputs(panel, context.GetOptionalArgument(InputsArgument)).TryPickProblems(out problems, out var inputs))
        {
            return problems;
        }

        if (length is < 0)
        {
            return new ResultProblem("length can't be negative");
        }

        var created = library.CreateSound(panel.Id, name, TimeSpan.FromSeconds(length ?? 0.3f), inputs);
        if (created.TryPickProblems(out problems, out var sound))
        {
            return problems;
        }

        return new CommandOutput(SfxArguments.Json(SfxLibraryCodec.ToDocument(sound), SfxCommandJsonContext.Default.SoundDocument));
    }

    private static Result<ImmutableDictionary<Id<PatchInput>, float>> ParseInputs(PatchPanel panel, string? text)
    {
        var inputs = ImmutableDictionary.CreateBuilder<Id<PatchInput>, float>();
        foreach (var pair in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return new ResultProblem("'{0}' isn't an input and a number, as in Pitch=440", pair);
            }

            var input = panel.Patch.Parameters.FirstOrDefault(input =>
                input.Name == parts[0] || (Id.TryParse<PatchInput>(parts[0], out var id) && input.Id == id));
            if (input is null)
            {
                return new ResultProblem("Panel '{0}' has no input '{1}'", panel.Name, parts[0]);
            }

            if (!inputs.TryAdd(input.Id, value))
            {
                return new ResultProblem("Input '{0}' is set more than once", input.Name);
            }
        }

        return inputs.ToImmutable();
    }
}
