using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class ExportSfxLibraryCommandHandler(SfxLibraryManager library, ILogger<ExportSfxLibraryCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument PathArgument = new("path", "JSON file to write", true);

    public string Verb => "export-sfx-library";
    public string HelpString => "Writes the whole SFX library (panels, sounds and bindings) to a JSON file";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [PathArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (library.CheckLoaded().TryPickProblems(out var problems)
            || context.GetRequiredArgument(PathArgument).TryPickProblems(out problems, out var path))
        {
            return problems;
        }

        var bytes = library.Export();
        try
        {
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to write '{0}': {1}", path, exception.Message);
        }

        logger.LogInformation("Exported the SFX library to '{Path}' ({Bytes} bytes)", path, bytes.Length);
        return new CommandOutput(SfxArguments.Json(new SfxFileJson(path, bytes.Length), SfxCommandJsonContext.Default.SfxFileJson));
    }
}
