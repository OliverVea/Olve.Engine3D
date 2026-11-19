using Microsoft.Extensions.Logging;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Platform.Desktop;

public class MarkLogCommandHandler(ILogger<MarkLogCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument Mark = new("mark", "text to log", Required: true);

    public string Verb => "mark-log";
    public string HelpString => "Logs the given mark, so a reader of the log knows the lines before it have been written";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [Mark];

    public Result<CommandOutput> Handle(CommandContext commandContext)
    {
        if (commandContext.GetRequiredArgument(Mark).TryPickProblems(out var problems, out var mark))
        {
            return problems;
        }

        logger.LogInformation("Log mark {Mark}", mark);
        return new CommandOutput($"Logged mark {mark}");
    }
}
