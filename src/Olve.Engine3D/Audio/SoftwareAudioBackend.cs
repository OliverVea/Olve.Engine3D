using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Audio;

public sealed class SoftwareAudioBackend(IAudioStreamBackend streams) : IAudioDeviceBackend, ISoundBackend
{
    private readonly Dictionary<SoundId, BakedSound> _sounds = [];

    public IReadOnlyList<AudioDeviceInfo> GetDevices() => streams.GetDevices();

    public Result<IAudioOutput> Open(string? deviceKey, AudioOutputSettings settings)
    {
        var output = new SoftwareAudioOutput(streams, _sounds);
        if (output.Reopen(deviceKey, settings).TryPickProblems(out var problems))
        {
            return problems;
        }

        return output;
    }

    public Result CreateSound(SoundId id, ReadOnlySpan<float> samples, int sampleRate)
    {
        _sounds[id] = new BakedSound(samples.ToArray(), sampleRate);
        return Result.Success();
    }

    public DeletionResult DeleteSound(SoundId id) =>
        _sounds.Remove(id) ? DeletionResult.Success() : DeletionResult.NotFound();
}

internal sealed class SoftwareAudioOutput(IAudioStreamBackend streams, IReadOnlyDictionary<SoundId, BakedSound> sounds) : IAudioOutput
{
    private readonly AudioEngine _engine = new();
    private IAudioDeviceStream? _stream;

    public string? DeviceKey => _stream?.DeviceKey;
    public AudioFormat? Format => _stream?.Format;
    public AudioOutputStatus Status => _stream is { IsLost: false } ? AudioOutputStatus.Playing : AudioOutputStatus.Lost;

    public void Post(AudioCommand command) =>
        _engine.Post(command, command is AudioCommand.Play play ? sounds.GetValueOrDefault(play.Sound) : null);

    public void Flush()
    {
    }

    public bool TryTakeEnded(out VoiceId voice) => _engine.TryTakeEnded(out voice);

    public Result Reopen(string? deviceKey, AudioOutputSettings settings)
    {
        _stream?.Dispose();
        _stream = null;
        if (streams.Open(deviceKey, settings, _engine).TryPickProblems(out var problems, out var stream))
        {
            return problems;
        }

        _stream = stream;
        return Result.Success();
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }
}
