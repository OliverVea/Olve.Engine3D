using System.Globalization;
using Microsoft.Extensions.Configuration;
using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// Reads <see cref="AudioStartupOptions"/> from <c>Audio:Outputs</c>: a list of outputs, each with a <c>Device</c>
/// (<c>default</c> or a key from <c>list-audio-devices</c>), and optionally a <c>Fallback</c>, <c>SampleRate</c>,
/// <c>LatencyMs</c>, <c>Channels</c> and the <c>Buses</c> it plays.
/// </summary>
public static class AudioConfiguration
{
    public const string SectionName = "Audio";
    public const string OutputsKey = "Outputs";

    public static Result<AudioStartupOptions> Bind(IConfiguration configuration)
    {
        var problems = new List<ResultProblem>();
        var outputs = new List<AudioStartupOutput>();
        foreach (var section in configuration.GetSection(SectionName).GetSection(OutputsKey).GetChildren())
        {
            if (section["Device"] is not { } device)
            {
                problems.Add(new ResultProblem("{0} needs a Device", section.Path));
                continue;
            }

            var fallback = Selection(section["Fallback"] ?? "default", new AudioDeviceSelection.Default());
            var settings = new AudioOutputSettings(
                Int(section, "SampleRate", problems),
                TimeSpan.FromMilliseconds(Int(section, "LatencyMs", problems) ?? 0),
                Int(section, "Channels", problems) ?? 2);
            var buses = section.GetSection("Buses").GetChildren()
                .Select(bus => bus.Value)
                .OfType<string>()
                .ToList();

            outputs.Add(new AudioStartupOutput(Selection(device, fallback), settings, buses));
        }

        if (problems.Count > 0)
        {
            return new ResultProblemCollection(problems);
        }

        return outputs.Count > 0 ? new AudioStartupOptions(outputs) : AudioStartupOptions.Default;
    }

    private static AudioDeviceSelection Selection(string text, AudioDeviceSelection fallback) =>
        text.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? new AudioDeviceSelection.Default()
            : new AudioDeviceSelection.Specific(text, fallback);

    private static int? Int(IConfigurationSection section, string key, List<ResultProblem> problems)
    {
        if (section[key] is not { } value)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        problems.Add(new ResultProblem("{0}:{1} is '{2}', expected a whole number", section.Path, key, value));
        return null;
    }
}
