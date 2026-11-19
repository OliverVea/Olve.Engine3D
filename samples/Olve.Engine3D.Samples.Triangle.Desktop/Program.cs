using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Math;
using Olve.Engine3D.Platform.Desktop;
using Olve.Engine3D.Samples.Triangle;

// `send <command>` sends a command to a running sample; `--listen` makes the sample accept commands;
// `--resolution <width>x<height>` fixes a borderless window size;
// `--samples <count>` sets multisampling; `--vsync <true|false>` turns VSync on or off.
const string instanceId = "triangle-sample";

if (args is ["send", var command])
{
    return await DesktopHost.SendCommandAsync(command, instanceId);
}

Vector2<int>? resolution = null;
if (GetOption("--resolution") is { } resolutionArg)
{
    var parts = resolutionArg.Split('x');
    if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height) || width <= 0 || height <= 0)
    {
        return await FailAsync($"Invalid resolution '{resolutionArg}'; expected <width>x<height>, e.g. 1920x1080");
    }

    resolution = new Vector2<int>(width, height);
}

var samples = 0;
if (GetOption("--samples") is { } samplesArg && (!int.TryParse(samplesArg, out samples) || samples < 0))
{
    return await FailAsync($"Invalid sample count '{samplesArg}'; expected a whole number of 0 or more");
}

var vSync = true;
if (GetOption("--vsync") is { } vSyncArg && !bool.TryParse(vSyncArg, out vSync))
{
    return await FailAsync($"Invalid VSync '{vSyncArg}'; expected true or false");
}

return DesktopHost.Run(
    new DesktopHostOptions
    {
        AppName = "Olve.Engine3D.Samples.Triangle",
        InstanceId = instanceId,
        Listen = args.Contains("--listen"),
        Resolution = resolution,
        Window = new DesktopWindowOptions { Title = "Triangle", Samples = samples, VSync = vSync },
    },
    services => services
        .AddLogging(logging => logging.AddConsole())
        .AddTriangleSample());

string? GetOption(string name) => args.SkipWhile(arg => arg != name).Skip(1).FirstOrDefault();

static async Task<int> FailAsync(string message)
{
    await Console.Error.WriteLineAsync(message);
    return 1;
}
