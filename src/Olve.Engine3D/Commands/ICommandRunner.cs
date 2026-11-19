namespace Olve.Engine3D.Commands;

public interface ICommandRunner
{
    /// <summary>Runs a command; it completes at once unless its handler is an <see cref="IAsyncCommandHandler"/>.</summary>
    ValueTask<Result<CommandOutput>> RunAsync(RunCommandRequest request);
}
