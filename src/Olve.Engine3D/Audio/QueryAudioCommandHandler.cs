using System.Text.Json;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Audio;

public class QueryAudioCommandHandler(AudioDeviceManager devices, AudioMixer mixer, SoundManager soundManager) : ICommandHandler
{
    public string Verb => "query-audio";
    public string HelpString => "Queries the audio outputs, the sounds ready and still rendering, the buses and the voices playing";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [];

    public Result<CommandOutput> Handle(CommandContext context) => new CommandOutput(JsonSerializer.Serialize(
        new AudioQuery(
            Outputs: devices.Outputs.Select(AudioDeviceJson.Output).ToArray(),
            DefaultOutput: devices.DefaultOutput?.ToString(),
            Sounds: soundManager.Count,
            RenderingSounds: soundManager.RenderingCount,
            Buses: mixer.Buses
                .OrderBy(bus => bus.Name.Value, StringComparer.Ordinal)
                .ThenBy(bus => bus.Id.Value)
                .Select(bus => new AudioQuery.Bus(bus.Id.ToString(), bus.Name.Value, bus.Gain, bus.Cutoff, bus.Route?.ToString()))
                .ToArray(),
            Voices: mixer.Voices
                .Select(voice => new AudioQuery.Voice(
                    voice.Id.ToString(),
                    voice.Sound.ToString(),
                    voice.Bus.ToString(),
                    voice.Output.ToString(),
                    voice.Settings.Gain,
                    voice.Settings.Pan,
                    voice.Settings.Pitch,
                    voice.Settings.Loop))
                .ToArray()),
        EngineJsonContext.Default.AudioQuery));
}

internal sealed record AudioQuery(
    AudioOutputJson[] Outputs,
    string? DefaultOutput,
    int Sounds,
    int RenderingSounds,
    AudioQuery.Bus[] Buses,
    AudioQuery.Voice[] Voices)
{
    internal sealed record Bus(string Id, string Name, float Gain, float? Cutoff, string? Route);

    internal sealed record Voice(
        string Id,
        string Sound,
        string Bus,
        string Output,
        float Gain,
        float Pan,
        float Pitch,
        bool Loop);
}
