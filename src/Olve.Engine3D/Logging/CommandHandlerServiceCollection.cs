using System.Collections;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Logging;

/// <summary>
/// The command handlers that can run: the services registered as <see cref="ICommandHandler"/>, plus scene handlers,
/// which add themselves while their scene is loaded.
/// </summary>
/// <param name="registeredHandlers">
/// Resolves the registered handlers. Called on first use, because several handlers depend on the command system
/// themselves (help lists the commands, step runs frames).
/// </param>
public class CommandHandlerServiceCollection(
    ILogger<CommandHandlerServiceCollection> logger,
    Func<IEnumerable<ICommandHandler>> registeredHandlers) : IEnumerable<ICommandHandler>
{
    private readonly HashSet<ICommandHandler> _commandHandlers = [];
    private bool _registeredHandlersAdded;

    private HashSet<ICommandHandler> CommandHandlers
    {
        get
        {
            if (!_registeredHandlersAdded)
            {
                _registeredHandlersAdded = true;
                foreach (var handler in registeredHandlers())
                {
                    AddTo(_commandHandlers, handler);
                }
            }

            return _commandHandlers;
        }
    }

    public bool Add(ICommandHandler commandHandler) => AddTo(CommandHandlers, commandHandler);

    public bool Remove(ICommandHandler commandHandler)
    {
        var serviceTypeName = commandHandler.GetType().Name;
        logger.LogDebug("[CommandHandlerServiceCollection] Deregistering {ServiceTypeName}", serviceTypeName);
        return CommandHandlers.Remove(commandHandler);
    }

    public IEnumerator<ICommandHandler> GetEnumerator() => CommandHandlers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool AddTo(HashSet<ICommandHandler> handlers, ICommandHandler commandHandler)
    {
        var serviceTypeName = commandHandler.GetType().Name;
        logger.LogDebug("[CommandHandlerServiceCollection] Registering {ServiceTypeName}", serviceTypeName);
        return handlers.Add(commandHandler);
    }
}
