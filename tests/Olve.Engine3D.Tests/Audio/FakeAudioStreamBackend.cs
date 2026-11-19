using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;
using Olve.Results;

namespace Olve.Engine3D.Tests.Audio;

/// <summary>Devices in memory: a test renders each open stream itself, as its audio thread would.</summary>
internal sealed class FakeAudioStreamBackend : IAudioStreamBackend
{
    public const int SampleRate = 48_000;

    public List<AudioDeviceInfo> Devices { get; } = [new("speakers", "Speakers", true), new("headphones", "Headphones", false)];

    /// <summary>Devices that fail to open.</summary>
    public HashSet<string> Broken { get; } = [];

    public List<FakeStream> Opened { get; } = [];

    public IReadOnlyList<AudioDeviceInfo> GetDevices() => Devices;

    public Result<IAudioDeviceStream> Open(string? deviceKey, AudioOutputSettings settings, IAudioStream stream)
    {
        var key = deviceKey ?? Devices.First(device => device.IsDefault).Key;
        if (Broken.Contains(key))
        {
            return new ResultProblem("'{0}' is broken", key);
        }

        var opened = new FakeStream(key, new AudioFormat(settings.SampleRate ?? SampleRate, settings.Channels), stream);
        Opened.Add(opened);
        return opened;
    }

    internal sealed class FakeStream(string deviceKey, AudioFormat format, IAudioStream stream) : IAudioDeviceStream
    {
        public string DeviceKey { get; } = deviceKey;
        public AudioFormat Format { get; } = format;
        public bool IsLost { get; set; }
        public bool IsDisposed { get; private set; }

        /// <summary>Renders blocks of 512 frames and returns each channel.</summary>
        public float[][] Render(int blocks)
        {
            var buffer = new SampleBuffer();
            buffer.Reset(Format.Channels, 512);
            var channels = Enumerable.Range(0, Format.Channels).Select(_ => new List<float>()).ToArray();
            for (var block = 0; block < blocks; block++)
            {
                stream.Render(buffer, Format.SampleRate);
                for (var channel = 0; channel < Format.Channels; channel++)
                {
                    channels[channel].AddRange(buffer.GetChannel(channel).ToArray());
                }
            }

            return [.. channels.Select(channel => channel.ToArray())];
        }

        public void Dispose() => IsDisposed = true;
    }
}
