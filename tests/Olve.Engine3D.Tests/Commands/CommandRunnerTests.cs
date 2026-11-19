using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Logging;
using Olve.Results;

namespace Olve.Engine3D.Tests.Commands;

public class CommandRunnerTests
{
    private sealed class LaterHandler(TaskCompletionSource<string> output) : IAsyncCommandHandler
    {
        public string Verb => "later";
        public string HelpString => "";
        public IReadOnlyList<CommandArgument> Arguments => [];

        public async ValueTask<Result<CommandOutput>> HandleAsync(CommandContext commandContext)
            => new CommandOutput(await output.Task);
    }

    private static CommandRunner Create(ICommandHandler handler) => new(
        new CommandHandlerServiceCollection(NullLogger<CommandHandlerServiceCollection>.Instance, () => [handler]),
        NullLogger<CommandRunner>.Instance);

    [Test]
    public async Task An_Asynchronous_Command_Replies_When_It_Completes()
    {
        var output = new TaskCompletionSource<string>();
        var runner = Create(new LaterHandler(output));
        var reply = new TaskCompletionSource<CommandResponse>();

        var running = runner.RunAndReplyAsync(new PendingCommand("later", reply));
        await Assert.That(reply.Task.IsCompleted).IsFalse();

        output.SetResult("done");
        await running;

        var response = await reply.Task;
        await Assert.That(response.Success).IsTrue();
        await Assert.That(response.Output).IsEqualTo("done");
    }

    [Test]
    public async Task Running_An_Asynchronous_Command_Synchronously_Fails()
    {
        ICommandHandler handler = new LaterHandler(new TaskCompletionSource<string>());

        await Assert.That(handler.Handle(new CommandContext(new Dictionary<string, string>())).TryPickProblems(out _)).IsTrue();
    }
}
