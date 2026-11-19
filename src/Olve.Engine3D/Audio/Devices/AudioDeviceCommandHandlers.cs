using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Devices;

/// <param name="Selection">The devices the output tries, in order; <c>default</c> is the system default.</param>
internal sealed record AudioOutputJson(
    string Id,
    string[] Selection,
    string? Device,
    int? SampleRate,
    int? Channels,
    int? RequestedSampleRate,
    double TargetLatencyMs,
    string Status);

internal sealed record AudioDeviceJsonEntry(string Key, string Name, bool IsDefault);

internal static class AudioDeviceJson
{
    public static string[] Selection(AudioDeviceSelection selection) => selection switch
    {
        AudioDeviceSelection.Specific specific => [specific.Key, .. Selection(specific.Fallback)],
        _ => ["default"],
    };

    public static AudioOutputJson Output(AudioOutputState state) => new(
        state.Id.ToString(),
        Selection(state.Selection),
        state.DeviceKey,
        state.Format?.SampleRate,
        state.Format?.Channels,
        state.Settings.SampleRate,
        state.Settings.TargetLatency.TotalMilliseconds,
        state.Status.ToString());
}

/// <summary>The arguments that choose a device and its settings, shared by opening and reconfiguring.</summary>
internal static class AudioOutputArguments
{
    public static readonly CommandArgument Device = new("device", "'default', or a device key from list-audio-devices", true);
    public static readonly CommandArgument Fallback = new("fallback", "A device key, or 'default' (the default), for while the device is missing");
    public static readonly CommandArgument SampleRate = new("sample-rate", "Hz; the device's own if not given");
    public static readonly CommandArgument Latency = new("latency", "Target latency in milliseconds; the backend's default if not given");
    public static readonly CommandArgument Channels = new("channels", "1 or 2 (default 2)");
    public static readonly CommandArgument Output = new("output", "The output's id", true);

    public static IReadOnlyList<CommandArgument> All { get; } = [Device, Fallback, SampleRate, Latency, Channels];

    public static Result<(AudioDeviceSelection Device, AudioOutputSettings Settings)> Parse(CommandContext context)
    {
        if (context.GetRequiredArgument(Device).TryPickProblems(out var problems, out var device)
            || ParseInt(context, SampleRate).TryPickProblems(out problems, out var sampleRate)
            || ParseInt(context, Latency).TryPickProblems(out problems, out var latency)
            || ParseInt(context, Channels).TryPickProblems(out problems, out var channels))
        {
            return problems;
        }

        var fallback = ParseSelection(context.GetOptionalArgument(Fallback) ?? "default", new AudioDeviceSelection.Default());
        var settings = new AudioOutputSettings(sampleRate, TimeSpan.FromMilliseconds(latency ?? 0), channels ?? 2);
        return (ParseSelection(device, fallback), settings);
    }

    public static Result<AudioOutputId> ParseOutput(CommandContext context)
    {
        if (context.GetRequiredArgument(Output).TryPickProblems(out var problems, out var text))
        {
            return problems;
        }

        return Id.TryParse(text, out var id) ? new AudioOutputId(id) : new ResultProblem("'{0}' isn't an output id", text);
    }

    /// <summary>'default' is the system default; anything else is a device key.</summary>
    public static AudioDeviceSelection ParseSelection(string text, AudioDeviceSelection fallback) =>
        text.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? new AudioDeviceSelection.Default()
            : new AudioDeviceSelection.Specific(text, fallback);

    private static Result<int?> ParseInt(CommandContext context, CommandArgument argument)
    {
        var text = context.GetOptionalArgument(argument);
        if (text is null)
        {
            return (int?)null;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : new ResultProblem("'{0}' isn't a whole number for {1}", text, argument.Name);
    }
}

public class ListAudioDevicesCommandHandler(AudioDeviceManager devices) : ICommandHandler
{
    public string Verb => "list-audio-devices";
    public string HelpString => "Lists the audio devices: their keys, names and which is the system default";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [];

    public Result<CommandOutput> Handle(CommandContext context) => new CommandOutput(JsonSerializer.Serialize(
        devices.Devices.Select(device => new AudioDeviceJsonEntry(device.Key, device.Name, device.IsDefault)).ToArray(),
        EngineJsonContext.Default.AudioDeviceJsonEntryArray));
}

public class OpenAudioOutputCommandHandler(AudioDeviceManager devices, ILogger<OpenAudioOutputCommandHandler> logger) : ICommandHandler
{
    public string Verb => "open-audio-output";
    public string HelpString => "Opens another audio output on a device; returns its id and state";
    public IReadOnlyList<CommandArgument> Arguments => AudioOutputArguments.All;

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (AudioOutputArguments.Parse(context).TryPickProblems(out var problems, out var parsed)
            || devices.Open(parsed.Device, parsed.Settings).TryPickProblems(out problems, out var id)
            || devices.GetState(id).TryPickProblems(out problems, out var state))
        {
            return problems;
        }

        logger.LogInformation("Opened audio output {OutputId} by command", id);
        return new CommandOutput(JsonSerializer.Serialize(AudioDeviceJson.Output(state), EngineJsonContext.Default.AudioOutputJson));
    }
}

public class ReconfigureAudioOutputCommandHandler(AudioDeviceManager devices, ILogger<ReconfigureAudioOutputCommandHandler> logger) : ICommandHandler
{
    public string Verb => "reconfigure-audio-output";
    public string HelpString => "Moves an audio output to another device or settings; its sounds carry on";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [AudioOutputArguments.Output, .. AudioOutputArguments.All];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (AudioOutputArguments.ParseOutput(context).TryPickProblems(out var problems, out var id)
            || AudioOutputArguments.Parse(context).TryPickProblems(out problems, out var parsed)
            || devices.Reconfigure(id, parsed.Device, parsed.Settings).TryPickProblems(out problems)
            || devices.GetState(id).TryPickProblems(out problems, out var state))
        {
            return problems;
        }

        logger.LogInformation("Reconfigured audio output {OutputId} by command", id);
        return new CommandOutput(JsonSerializer.Serialize(AudioDeviceJson.Output(state), EngineJsonContext.Default.AudioOutputJson));
    }
}

public class CloseAudioOutputCommandHandler(AudioDeviceManager devices, ILogger<CloseAudioOutputCommandHandler> logger) : ICommandHandler
{
    public string Verb => "close-audio-output";
    public string HelpString => "Closes an audio output; its buses move to the default output";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [AudioOutputArguments.Output];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (AudioOutputArguments.ParseOutput(context).TryPickProblems(out var problems, out var id)
            || devices.Close(id).TryPickProblems(out problems))
        {
            return problems;
        }

        logger.LogInformation("Closed audio output {OutputId} by command", id);
        return new CommandOutput($"Closed audio output {id}");
    }
}

public class RouteAudioBusCommandHandler(AudioMixer mixer, ILogger<RouteAudioBusCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument NameArgument = new("name", "The bus's name", true);

    public string Verb => "route-audio-bus";
    public string HelpString => "Plays a bus on an audio output from now on";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [NameArgument, AudioOutputArguments.Output];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (context.GetRequiredArgument(NameArgument).TryPickProblems(out var problems, out var name)
            || AudioOutputArguments.ParseOutput(context).TryPickProblems(out problems, out var output))
        {
            return problems;
        }

        if (mixer.Buses.FirstOrDefault(bus => bus.Name == new BusName(name)) is not { } bus)
        {
            return new ResultProblem("No audio bus '{0}'", name);
        }

        if (mixer.Route(bus.Id, output).TryPickProblems(out problems))
        {
            return problems;
        }

        logger.LogInformation("Routed audio bus '{Bus}' to output {OutputId}", name, output);
        return new CommandOutput($"Routed audio bus '{name}' to output {output}");
    }
}
