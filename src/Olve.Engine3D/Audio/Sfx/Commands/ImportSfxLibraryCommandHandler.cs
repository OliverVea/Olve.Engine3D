using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class ImportSfxLibraryCommandHandler(SfxLibraryManager library) : ICommandHandler
{
    private static readonly CommandArgument PathArgument = new("path", "JSON file to read, as export-sfx-library writes", true);

    public string Verb => "import-sfx-library";
    public string HelpString => "Merges an exported SFX library into this one: its panels, sounds and bindings are added, or replace the ones with the same id";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [PathArgument, SfxArguments.DryRun];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (library.CheckLoaded().TryPickProblems(out var problems)
            || context.GetRequiredArgument(PathArgument).TryPickProblems(out problems, out var path)
            || SfxArguments.Flag(context, SfxArguments.DryRun).TryPickProblems(out problems, out var dryRun))
        {
            return problems;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to read '{0}': {1}", path, exception.Message);
        }

        SfxImport summary;
        if (dryRun)
        {
            if (library.PlanImport(bytes).TryPickProblems(out problems, out var plan))
            {
                return problems;
            }

            summary = plan.Summary;
        }
        else
        {
            if (library.Import(bytes).TryPickProblems(out problems, out var imported))
            {
                return problems;
            }

            summary = imported;
        }

        return new CommandOutput(SfxArguments.Json(new SfxImportJson(dryRun, summary), SfxCommandJsonContext.Default.SfxImportJson));
    }
}
