using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Audio.Devices;

/// <summary>Opens the outputs in <see cref="AudioStartupOptions"/> when the game loads, and routes their buses.</summary>
public sealed class AudioOutputStartup(
    AudioDeviceManager devices,
    AudioMixer mixer,
    AudioStartupOptions options,
    ILogger<AudioOutputStartup> logger)
{
    private static readonly AudioStartupOutput DefaultOutput = new(new AudioDeviceSelection.Default(), new AudioOutputSettings(), []);

    /// <summary>Opens the outputs; one that fails is logged and skipped, and with none open the game runs silently.</summary>
    public void Open()
    {
        if (devices.Devices.Count == 0)
        {
            logger.LogDebug("There are no audio devices; audio is silent");
            return;
        }

        var outputs = options.Outputs.Count > 0 ? options.Outputs : [DefaultOutput];
        foreach (var output in outputs)
        {
            if (devices.Open(output.Device, output.Settings).TryPickProblems(out var problems, out var id))
            {
                logger.LogWarning("Audio output on {Device} unavailable:{NewLine}{Problems}",
                    output.Device, Environment.NewLine, string.Join(Environment.NewLine, problems.Select(p => p.ToDebugString())));
                continue;
            }

            foreach (var bus in output.Buses)
            {
                if (mixer.RouteByName(new BusName(bus), id).TryPickProblems(out var routeProblems))
                {
                    logger.LogWarning("Failed to route audio bus '{Bus}' to output {OutputId}: {Problems}", bus, id, routeProblems);
                }
            }
        }

        if (devices.DefaultOutput is null)
        {
            logger.LogWarning("No audio output opened; the game runs silently");
        }
    }
}
