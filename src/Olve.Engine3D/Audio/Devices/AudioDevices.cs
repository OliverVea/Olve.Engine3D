using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Devices;

/// <param name="Key">Identifies the device across runs, as long as it stays connected.</param>
public sealed record AudioDeviceInfo(string Key, string Name, bool IsDefault);

/// <summary>Which device an output plays on.</summary>
public abstract record AudioDeviceSelection
{
    // Closed: the cases below are the only ones.
    private AudioDeviceSelection()
    {
    }

    /// <summary>The system's default device.</summary>
    public sealed record Default : AudioDeviceSelection;

    /// <summary>The device with the key, or the fallback while it's missing or fails to open.</summary>
    public sealed record Specific(string Key, AudioDeviceSelection Fallback) : AudioDeviceSelection;
}

/// <param name="SampleRate">The rate to play at; null for the device's own.</param>
/// <param name="TargetLatency">How much audio is queued ahead; zero for the backend's default.</param>
/// <param name="Channels">1 (mono) or 2 (stereo).</param>
public sealed record AudioOutputSettings(int? SampleRate = null, TimeSpan TargetLatency = default, int Channels = 2);

/// <summary>An output opened by the <see cref="AudioDeviceManager"/>; it keeps its id while it's reconfigured.</summary>
public readonly record struct AudioOutputId(Id Value)
{
    public static AudioOutputId New() => new(Id.New());
    public override string ToString() => Value.ToString();
}

public enum AudioOutputStatus
{
    Playing,

    Suspended,

    /// <summary>The device stopped playing (unplugged, or no device could be opened); the output stays silent.</summary>
    Lost,
}

/// <param name="DeviceKey">The device playing, or null while lost.</param>
/// <param name="Format">The format playing, or null while lost.</param>
public sealed record AudioOutputState(
    AudioOutputId Id,
    AudioDeviceSelection Selection,
    AudioOutputSettings Settings,
    string? DeviceKey,
    AudioFormat? Format,
    AudioOutputStatus Status);

/// <summary>The platform's audio devices: lists them and opens outputs on them.</summary>
public interface IAudioDeviceBackend
{
    IReadOnlyList<AudioDeviceInfo> GetDevices();

    /// <summary>Opens the device, the system default for a null key.</summary>
    Result<IAudioOutput> Open(string? deviceKey, AudioOutputSettings settings);
}

public interface IAudioOutput : IDisposable
{
    string? DeviceKey { get; }
    AudioFormat? Format { get; }
    AudioOutputStatus Status { get; }

    void Post(AudioCommand command);

    void Flush();

    bool TryTakeEnded(out VoiceId voice);

    Result Reopen(string? deviceKey, AudioOutputSettings settings);
}

/// <summary>A platform's audio devices that play a stream: lists them and plays a stream on one.</summary>
public interface IAudioStreamBackend
{
    IReadOnlyList<AudioDeviceInfo> GetDevices();

    /// <summary>
    /// Opens the device (the system default for a null key) and renders the stream on an audio thread of its own
    /// until the device stream is disposed.
    /// </summary>
    Result<IAudioDeviceStream> Open(string? deviceKey, AudioOutputSettings settings, IAudioStream stream);
}

/// <summary>A device playing a stream. Disposing it stops the audio thread and closes the device.</summary>
public interface IAudioDeviceStream : IDisposable
{
    string DeviceKey { get; }
    AudioFormat Format { get; }

    /// <summary>Whether the device stopped playing, e.g. because it was unplugged.</summary>
    bool IsLost { get; }
}

/// <param name="Buses">The names of the buses routed to the output.</param>
public sealed record AudioStartupOutput(AudioDeviceSelection Device, AudioOutputSettings Settings, IReadOnlyList<string> Buses);

/// <summary>The outputs opened when the game starts; none configured opens one on the default device.</summary>
public sealed record AudioStartupOptions(IReadOnlyList<AudioStartupOutput> Outputs)
{
    public static AudioStartupOptions Default { get; } = new([]);
}

/// <summary>An audio host without devices: there is nothing to open, so the game runs silently.</summary>
public sealed class SilentAudioDeviceBackend : IAudioDeviceBackend
{
    public IReadOnlyList<AudioDeviceInfo> GetDevices() => [];

    public Result<IAudioOutput> Open(string? deviceKey, AudioOutputSettings settings) =>
        new ResultProblem("The host has no audio devices");
}
