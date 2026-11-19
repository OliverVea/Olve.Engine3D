using System.Text;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Storage;
using Olve.Results;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Samples.Triangle;

public class LaunchCounterService(IUserStorage storage, ILogger<LaunchCounterService> logger) : ISceneService
{
    private static readonly UserStorageKey Launches = new(
        Id.FromName<UserStorageArea>("triangle-sample"), Id.FromName<UserStorageEntry>("launches"));

    public Result Load()
    {
        _ = CountAsync(Launches);
        return Result.Success();
    }

    private async Task CountAsync(UserStorageKey key)
    {
        var listed = await storage.ListAsync(key.Area, CancellationToken.None);
        if (listed.TryPickProblems(out var problems, out var entries)
            || (await CountAsync(key, CancellationToken.None)).TryPickProblems(out problems, out var launches))
        {
            logger.LogError("Failed to count launches: {Problems}", string.Join("; ", problems.Select(p => p.ToBriefString())));
            return;
        }

        if (entries.FirstOrDefault(entry => entry.Key == key) is { } previous)
        {
            logger.LogInformation("The previous launch was at {Modified:u}", previous.Modified);
        }

        logger.LogInformation("Launch {Launches}", launches);
    }

    private async Task<Result<int>> CountAsync(UserStorageKey key, CancellationToken cancellationToken)
    {
        var previous = 0;
        var read = await storage.ReadAsync(key, cancellationToken);
        if (read.TryPickProblems(out var problems, out var bytes))
        {
            if (!read.TryPickProblem<NotFoundProblem>(out _))
            {
                return problems;
            }
        }
        else if (!int.TryParse(Encoding.UTF8.GetString(bytes), out previous))
        {
            return new ResultProblem("The launch counter holds '{0}', not a number", Encoding.UTF8.GetString(bytes));
        }

        var launches = previous + 1;
        return (await storage.WriteAsync(key, Encoding.UTF8.GetBytes(launches.ToString()), cancellationToken))
            .WithValueOnSuccess(launches);
    }
}
