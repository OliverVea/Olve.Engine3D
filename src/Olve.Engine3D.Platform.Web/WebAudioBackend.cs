using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Platform.Web;

public sealed partial class WebAudioBackend(Uri moduleUri, ILogger<WebAudioBackend> logger) : IAudioDeviceBackend, ISoundBackend
{
    internal const string ModuleName = "Olve.Engine3D.Platform.Web.WebAudio";
    private const string DeviceKey = "default";

    private readonly Dictionary<SoundId, int> _sounds = [];
    private bool _available;
    private int _nextSound = 1;

    public async Task LoadAsync()
    {
        try
        {
            await JSHost.ImportAsync(ModuleName, moduleUri.ToString());
            _available = IsAvailable();
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "Web Audio is unavailable; the game runs silently");
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetDevices() => _available ? [new AudioDeviceInfo(DeviceKey, "Default", true)] : [];

    public Result<IAudioOutput> Open(string? deviceKey, AudioOutputSettings settings)
    {
        var output = new WebAudioOutput(this);
        if (output.Reopen(deviceKey, settings).TryPickProblems(out var problems))
        {
            return problems;
        }

        return output;
    }

    public Result CreateSound(SoundId id, ReadOnlySpan<float> samples, int sampleRate)
    {
        if (!_available)
        {
            return Result.Success();
        }

        var handle = _nextSound++;
        try
        {
            var bytes = MemoryMarshal.AsBytes(samples);
            CreateSoundBuffer(handle, MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bytes), bytes.Length), sampleRate);
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to create sound {0}", id);
        }

        _sounds[id] = handle;
        return Result.Success();
    }

    public DeletionResult DeleteSound(SoundId id)
    {
        if (!_sounds.Remove(id, out var handle))
        {
            return DeletionResult.NotFound();
        }

        DeleteSoundBuffer(handle);
        return DeletionResult.Success();
    }

    internal bool TryGetSound(SoundId id, out int handle) => _sounds.TryGetValue(id, out handle);

    internal static Result<int> OpenContext(string? deviceKey, AudioOutputSettings settings)
    {
        if (deviceKey is not (null or DeviceKey))
        {
            return new ResultProblem("No audio device '{0}'", deviceKey);
        }

        try
        {
            return Open(settings.SampleRate ?? 0, settings.TargetLatency.TotalSeconds, settings.Channels);
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to create an audio context");
        }
    }

    internal static AudioOutputStatus Status(int context) => State(context) switch
    {
        "running" => AudioOutputStatus.Playing,
        "closed" => AudioOutputStatus.Lost,
        _ => AudioOutputStatus.Suspended,
    };

    [JSImport("isAvailable", ModuleName)]
    private static partial bool IsAvailable();

    [JSImport("createSound", ModuleName)]
    private static partial void CreateSoundBuffer(int sound, [JSMarshalAs<JSType.MemoryView>] Span<byte> samples, int sampleRate);

    [JSImport("deleteSound", ModuleName)]
    private static partial void DeleteSoundBuffer(int sound);

    [JSImport("open", ModuleName)]
    private static partial int Open(int sampleRate, double latencySeconds, int channels);

    [JSImport("close", ModuleName)]
    internal static partial void Close(int context);

    [JSImport("state", ModuleName)]
    private static partial string State(int context);

    [JSImport("sampleRate", ModuleName)]
    internal static partial int SampleRate(int context);

    [JSImport("channels", ModuleName)]
    internal static partial int Channels(int context);

    [JSImport("submit", ModuleName)]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    internal static partial int[] Submit(int context, [JSMarshalAs<JSType.Array<JSType.Number>>] double[] commands);
}
