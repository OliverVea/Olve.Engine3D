namespace Olve.Engine3D.Commands;

/// <summary>
/// A command that finishes later, e.g. after frames have run or a file was read. Its reply is sent when
/// <see cref="HandleAsync"/> completes; awaits in it resume on the main thread at the start of a frame.
/// </summary>
public interface IAsyncCommandHandler : ICommandHandler
{
    ValueTask<Result<CommandOutput>> HandleAsync(CommandContext commandContext);

    Result<CommandOutput> ICommandHandler.Handle(CommandContext commandContext) =>
        new ResultProblem("Command '{0}' is asynchronous; run it through CommandRunner.RunAsync", Verb);
}
