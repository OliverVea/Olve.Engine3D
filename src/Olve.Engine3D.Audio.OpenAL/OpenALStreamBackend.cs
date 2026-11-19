using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Devices;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;

namespace Olve.Engine3D.Audio.OpenAL;

/// <summary>
/// The audio devices through OpenAL Soft. Each output opens its own device and context, made current only on its own
/// refill thread (<c>ALC_EXT_thread_local_context</c>), so outputs never wait on each other.
/// </summary>
public sealed unsafe class OpenALStreamBackend(ILoggerFactory loggerFactory) : IAudioStreamBackend, IDisposable
{
    private const string EnumerateAllExtension = "ALC_ENUMERATE_ALL_EXT";
    private const string ThreadLocalContextExtension = "ALC_EXT_thread_local_context";
    private const int DefaultAllDevicesSpecifier = 0x1012;
    internal const int AllDevicesSpecifier = 0x1013;

    /// <summary>OpenAL Soft names every device after itself; the rest of the name is the device's own.</summary>
    private const string NamePrefix = "OpenAL Soft on ";

    private readonly ILogger<OpenALStreamBackend> _logger = loggerFactory.CreateLogger<OpenALStreamBackend>();
    private OpenALApi? _api;

    public IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        if (GetApi().TryPickProblems(out var problems, out var api))
        {
            _logger.LogDebug("No audio devices: {Problems}", problems);
            return [];
        }

        var defaultKey = ReadString(api.GetString(null, DefaultAllDevicesSpecifier));
        var keys = ReadList(api.GetString(null, AllDevicesSpecifier));
        return [.. keys.Select(key => new AudioDeviceInfo(
            key,
            key.StartsWith(NamePrefix, StringComparison.Ordinal) ? key[NamePrefix.Length..] : key,
            key == defaultKey))];
    }

    public Result<IAudioDeviceStream> Open(string? deviceKey, AudioOutputSettings settings, IAudioStream stream)
    {
        if (GetApi().TryPickProblems(out var problems, out var api))
        {
            return problems;
        }

        return OpenALDeviceStream.Open(api, deviceKey, settings, stream, loggerFactory.CreateLogger<OpenALDeviceStream>());
    }

    public void Dispose()
    {
        _api?.Dispose();
        _api = null;
    }

    private Result<OpenALApi> GetApi()
    {
        if (_api is not null)
        {
            return _api;
        }

        if (Result.Try<ALContext, Exception>(() => ALContext.GetApi(soft: true), "Error while loading OpenAL Soft")
            .TryPickProblems(out var problems, out var alc))
        {
            return problems;
        }

        if (Result.Try<AL, Exception>(() => AL.GetApi(soft: true), "Error while loading OpenAL Soft")
            .TryPickProblems(out problems, out var al))
        {
            alc.Dispose();
            return problems;
        }

        foreach (var extension in new[] { EnumerateAllExtension, ThreadLocalContextExtension })
        {
            if (!alc.IsExtensionPresent(null, extension))
            {
                al.Dispose();
                alc.Dispose();
                return new ResultProblem("OpenAL Soft doesn't support {0}", extension);
            }
        }

        _api = new OpenALApi(alc, al);
        return _api;
    }

    internal static string? ReadString(byte* text) => text is null ? null : new string((sbyte*)text);

    // A list of strings, each ended by a null, the whole ended by another
    private static List<string> ReadList(byte* list)
    {
        List<string> strings = [];
        while (list is not null && *list != 0)
        {
            var text = new string((sbyte*)list);
            strings.Add(text);
            list += System.Text.Encoding.UTF8.GetByteCount(text) + 1;
        }

        return strings;
    }
}

/// <summary>OpenAL Soft's entry points, including the ALC extension functions Silk.NET doesn't wrap.</summary>
internal sealed unsafe class OpenALApi : IDisposable
{
    private readonly delegate* unmanaged[Cdecl]<Device*, int, byte*> _getString;
    private readonly delegate* unmanaged[Cdecl]<Context*, bool> _setThreadContext;
    private readonly delegate* unmanaged[Cdecl]<Device*, int, int, int*, void> _getIntegers;

    public OpenALApi(ALContext alc, AL al)
    {
        Alc = alc;
        Al = al;
        _getString = (delegate* unmanaged[Cdecl]<Device*, int, byte*>)alc.GetProcAddress(null, "alcGetString");
        _setThreadContext = (delegate* unmanaged[Cdecl]<Context*, bool>)alc.GetProcAddress(null, "alcSetThreadContext");
        _getIntegers = (delegate* unmanaged[Cdecl]<Device*, int, int, int*, void>)alc.GetProcAddress(null, "alcGetIntegerv");
    }

    public ALContext Alc { get; }
    public AL Al { get; }

    public byte* GetString(Device* device, int parameter) => _getString(device, parameter);

    /// <summary>Makes the context current on the calling thread only; null clears it.</summary>
    public bool SetThreadContext(Context* context) => _setThreadContext(context);

    public int GetInteger(Device* device, int parameter)
    {
        var value = 0;
        _getIntegers(device, parameter, 1, &value);
        return value;
    }

    public void Dispose()
    {
        Al.Dispose();
        Alc.Dispose();
    }
}

/// <summary>One output: a device, a context, a source and its queue of buffers, refilled on a thread of its own.</summary>
internal sealed unsafe class OpenALDeviceStream : IAudioDeviceStream
{
    /// <summary>Frames per queued buffer: small, so a low target latency can be met.</summary>
    public const int BufferFrames = 256;

    private const string Float32Extension = "AL_EXT_FLOAT32";
    private const string DisconnectExtension = "ALC_EXT_disconnect";
    private const int FrequencyAttribute = 0x1007;

    // ALC_REFRESH: how many times a second OpenAL Soft mixes; more often means a smaller buffer of its own
    private const int RefreshAttribute = 0x1008;
    private const int RefreshRate = 200;
    private const int ConnectedParameter = 0x313;
    private static readonly TimeSpan DefaultLatency = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan RefillPeriod = TimeSpan.FromMilliseconds(1);

    private readonly OpenALApi _api;
    private readonly IAudioStream _stream;
    private readonly ILogger<OpenALDeviceStream> _logger;
    private readonly SampleBuffer _block = new();
    private readonly Stack<uint> _freeBuffers = new();
    private readonly bool _canDisconnect;

    private Device* _device;
    private Context* _context;
    private uint _source;
    private uint[] _buffers = [];
    private uint[] _unqueued = [];
    private float[] _interleaved = [];
    private Thread? _thread;
    private volatile bool _running;
    private volatile bool _lost;
    private bool _started;
    private int _underruns;

    private OpenALDeviceStream(OpenALApi api, Device* device, IAudioStream stream, ILogger<OpenALDeviceStream> logger)
    {
        _api = api;
        _device = device;
        _stream = stream;
        _logger = logger;
        _canDisconnect = api.Alc.IsExtensionPresent(device, DisconnectExtension);
        // The full name of the device that opened, which is also its key in the device list
        DeviceKey = OpenALStreamBackend.ReadString(api.GetString(device, OpenALStreamBackend.AllDevicesSpecifier)) ?? string.Empty;
    }

    public string DeviceKey { get; }
    public AudioFormat Format { get; private set; } = new(0, 0);
    public bool IsLost => _lost;

    public static Result<IAudioDeviceStream> Open(
        OpenALApi api, string? deviceKey, AudioOutputSettings settings, IAudioStream stream, ILogger<OpenALDeviceStream> logger)
    {
        var device = api.Alc.OpenDevice(deviceKey!);
        if (device is null)
        {
            return new ResultProblem("Could not open audio device '{0}'", deviceKey ?? "default");
        }

        var output = new OpenALDeviceStream(api, device, stream, logger);
        if (output.Start(settings).TryPickProblems(out var problems))
        {
            output.Dispose();
            return problems;
        }

        return output;
    }

    private Result Start(AudioOutputSettings settings)
    {
        int[] attributes = settings.SampleRate is { } rate
            ? [FrequencyAttribute, rate, RefreshAttribute, RefreshRate, 0]
            : [RefreshAttribute, RefreshRate, 0];
        fixed (int* attributePointer = attributes)
        {
            _context = _api.Alc.CreateContext(_device, attributePointer);
        }

        if (_context is null || !_api.SetThreadContext(_context))
        {
            return new ResultProblem("Could not create an OpenAL context on '{0}'", DeviceKey);
        }

        try
        {
            var al = _api.Al;
            if (!al.IsExtensionPresent(Float32Extension))
            {
                return new ResultProblem("'{0}' doesn't support {1}", DeviceKey, Float32Extension);
            }

            var sampleRate = _api.GetInteger(_device, FrequencyAttribute);
            Format = new AudioFormat(sampleRate, settings.Channels);

            var latency = settings.TargetLatency > TimeSpan.Zero ? settings.TargetLatency : DefaultLatency;
            var bufferCount = int.Max(2, (int)double.Ceiling(latency.TotalSeconds * sampleRate / BufferFrames));
            _source = al.GenSource();
            _buffers = al.GenBuffers(bufferCount);
            _unqueued = new uint[bufferCount];
            _interleaved = new float[BufferFrames * settings.Channels];
            foreach (var buffer in _buffers)
            {
                _freeBuffers.Push(buffer);
            }
        }
        finally
        {
            _api.SetThreadContext(null);
        }

        _block.Reset(Format.Channels, BufferFrames);
        _running = true;
        _thread = new Thread(Run) { Name = $"Audio: {DeviceKey}", IsBackground = true, Priority = ThreadPriority.Highest };
        _thread.Start();
        return Result.Success();
    }

    // The audio thread: keeps every buffer of the queue full, and the source playing
    private void Run()
    {
        _api.SetThreadContext(_context);
        try
        {
            while (_running)
            {
                if (_canDisconnect && _api.GetInteger(_device, ConnectedParameter) == 0)
                {
                    _logger.LogWarning("Audio device '{DeviceKey}' disconnected", DeviceKey);
                    _lost = true;
                    return;
                }

                Refill();
                Thread.Sleep(RefillPeriod);
            }
        }
        catch (Exception exception)
        {
            _lost = true;
            _logger.LogError(exception, "The audio thread of '{DeviceKey}' failed; it's silent from now on", DeviceKey);
        }
        finally
        {
            _api.SetThreadContext(null);
        }
    }

    private void Refill()
    {
        var al = _api.Al;
        al.GetSourceProperty(_source, GetSourceInteger.BuffersProcessed, out var processed);
        if (processed > 0)
        {
            fixed (uint* unqueued = _unqueued)
            {
                al.SourceUnqueueBuffers(_source, processed, unqueued);
            }

            for (var i = 0; i < processed; i++)
            {
                _freeBuffers.Push(_unqueued[i]);
            }
        }

        var channels = Format.Channels;
        var bufferFormat = channels == 1 ? FloatBufferFormat.Mono : FloatBufferFormat.Stereo;
        while (_freeBuffers.Count > 0)
        {
            _stream.Render(_block, Format.SampleRate);
            for (var channel = 0; channel < channels; channel++)
            {
                var samples = _block.GetChannel(channel);
                for (var frame = 0; frame < BufferFrames; frame++)
                {
                    _interleaved[frame * channels + channel] = samples[frame];
                }
            }

            var alBuffer = _freeBuffers.Pop();
            fixed (float* data = _interleaved)
            {
                al.BufferData(alBuffer, bufferFormat, data, _interleaved.Length * sizeof(float), Format.SampleRate);
            }

            al.SourceQueueBuffers(_source, 1, &alBuffer);
        }

        al.GetSourceProperty(_source, GetSourceInteger.SourceState, out var state);
        if ((SourceState)state != SourceState.Playing)
        {
            // A source that stops after it started ran out of queued audio: the refill came too late, and it clicked
            if (_started && ++_underruns is 1 or 10 or 100 or 1000)
            {
                _logger.LogWarning("Audio output on '{DeviceKey}' ran dry {Count} times; its latency may be too low", DeviceKey, _underruns);
            }

            _started = true;
            al.SourcePlay(_source);
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join();
        _thread = null;

        var alc = _api.Alc;
        if (_context is not null)
        {
            if (_api.SetThreadContext(_context))
            {
                var al = _api.Al;
                al.SourceStop(_source);
                al.DeleteSource(_source);
                al.DeleteBuffers(_buffers);
                _api.SetThreadContext(null);
            }

            alc.DestroyContext(_context);
            _context = null;
        }

        if (_device is not null)
        {
            alc.CloseDevice(_device);
            _device = null;
        }

        _buffers = [];
        _freeBuffers.Clear();
    }
}
