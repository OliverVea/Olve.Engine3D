using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Logging;
using Olve.Results;

namespace Olve.Engine3D.Tests.Commands;

public class CommandHandlerServiceCollectionTests
{
    private sealed class FakeHandler(string verb) : ICommandHandler
    {
        public string Verb => verb;
        public string HelpString => "";
        public IReadOnlyList<CommandArgument> Arguments => [];
        public Result<CommandOutput> Handle(CommandContext commandContext) => new CommandOutput("");
    }

    [Test]
    public async Task Registered_handlers_are_resolved_on_first_use()
    {
        var resolved = 0;
        var collection = new CommandHandlerServiceCollection(
            NullLogger<CommandHandlerServiceCollection>.Instance,
            () =>
            {
                resolved++;
                return [new FakeHandler("echo")];
            });

        await Assert.That(resolved).IsEqualTo(0);

        var verbs = collection.Select(h => h.Verb).ToList();
        _ = collection.ToList();

        await Assert.That(verbs).IsEquivalentTo(["echo"]);
        await Assert.That(resolved).IsEqualTo(1);
    }

    [Test]
    public async Task Scene_handlers_join_and_leave_the_registered_ones()
    {
        var sceneHandler = new FakeHandler("place-track");
        var collection = new CommandHandlerServiceCollection(
            NullLogger<CommandHandlerServiceCollection>.Instance,
            () => [new FakeHandler("echo")]);

        collection.Add(sceneHandler);
        await Assert.That(collection.Select(h => h.Verb)).IsEquivalentTo(["echo", "place-track"]);

        collection.Remove(sceneHandler);
        await Assert.That(collection.Select(h => h.Verb)).IsEquivalentTo(["echo"]);
    }
}
