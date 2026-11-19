using Microsoft.Extensions.Logging;
using Olve.Engine3D.Diagnostics;
using Olve.Engine3D.Scenes;

namespace Olve.Engine3D.Commands;

public class CommandProcessingService(
    CommandQueue commandQueue,
    CommandRunner commandRunner) : ISceneService
{
    public int Priority => -1000;

    public Result Update()
    {
        while (commandQueue.TryDequeue(out var pendingCommand))
        {
            if (EngineMetrics.IsEnabled) EngineMetrics.CommandsProcessed.Add(1);

            // An asynchronous command replies in a later frame; the others reply now
            _ = commandRunner.RunAndReplyAsync(pendingCommand);
        }

        return Result.Success();
    }
}
