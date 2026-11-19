using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Olve.Engine3D;

public static class GameHost
{
    public static ServiceProvider BuildServiceProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

    public static async Task<Result> RunAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<GameManager>>();
        try
        {
            return await services.GetRequiredService<GameManager>().RunAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Unhandled exception in game loop");
            throw;
        }
    }
}
