using Microsoft.Extensions.Logging;
using Olve.Engine3D.Logging;

namespace Olve.Engine3D.Commands;

public class CommandRunner(CommandHandlerServiceCollection commandHandlers, ILogger<CommandRunner> logger) : ICommandRunner
{
    public async ValueTask<Result<CommandOutput>> RunAsync(RunCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return new ResultProblem("Command cannot be empty");
        }

        if (CommandLineParser.ParseVerbAndArgs(request.Command).TryPickProblems(out var problems, out var verbAndArgs))
        {
            return problems;
        }

        var (verb, args) = verbAndArgs;

        var handler = commandHandlers.FirstOrDefault(h => h.Verb == verb);
        if (handler is null)
        {
            return new ResultProblem("No handler found for verb '{0}'", verb);
        }

        var handlerArgs = handler.Arguments.ToDictionary(x => x.Key);

        foreach (var arg in args.Keys.Where(arg => !handlerArgs.ContainsKey(arg)))
        {
            return new ResultProblem("Command '{0}' does not have an argument '{1}'", verb, arg);
        }

        foreach (var handlerArg in handlerArgs.Values)
        {
            if (handlerArg.Required && !args.ContainsKey(handlerArg.Key))
            {
                return new ResultProblem("Command '{0}' requires the argument '{1}'", verb, handlerArg.Key);
            }
        }

        Result<CommandOutput> lastResult = CommandOutput.Empty;
        for (var i = 0; i < request.Times; i++)
        {
            var context = new CommandContext(args);
            lastResult = handler is IAsyncCommandHandler asyncHandler
                ? await asyncHandler.HandleAsync(context)
                : handler.Handle(context);
            if (lastResult.TryPickProblems(out problems)) return problems;
        }

        logger.LogDebug("Ran '{Command}' successfully", request.Command);

        return lastResult;
    }

    /// <summary>
    /// Runs a queued command and replies to whoever sent it once it completes, logging a failure. Doesn't throw, so
    /// callers can start it without awaiting it.
    /// </summary>
    public async Task RunAndReplyAsync(PendingCommand pending)
    {
        try
        {
            var result = await RunAsync(new RunCommandRequest(pending.Command));
            if (result.TryPickProblems(out var problems, out var output))
            {
                var errors = problems.Select(p => p.ToDebugString()).ToArray();
                logger.LogWarning("Command '{Command}' failed: {Errors}", pending.Command, string.Join("; ", errors));
                pending.CompletionSource.SetResult(new CommandResponse(false, string.Empty, errors));
            }
            else
            {
                pending.CompletionSource.SetResult(new CommandResponse(true, output.Text, []));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception executing command '{Command}'", pending.Command);
            pending.CompletionSource.SetResult(new CommandResponse(false, string.Empty, [ex.Message]));
        }
    }
}
