using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Platform.Web;

internal sealed class WebAudioOutput(WebAudioBackend backend) : IAudioOutput
{
    private const int Play = 0;
    private const int Release = 1;
    private const int SetVoice = 2;
    private const int StopVoice = 3;
    private const int SetBus = 4;
    private const int RemoveBus = 5;

    private readonly List<double> _commands = [];
    private readonly Dictionary<VoiceId, int> _voices = [];
    private readonly Dictionary<int, VoiceId> _voicesByHandle = [];
    private readonly Dictionary<BusId, Bus> _buses = [];
    private readonly Queue<VoiceId> _ended = [];
    private int _context;
    private int _nextHandle = 1;

    public string? DeviceKey => _context == 0 ? null : "default";

    public AudioFormat? Format => _context == 0
        ? null
        : new AudioFormat(WebAudioBackend.SampleRate(_context), WebAudioBackend.Channels(_context));

    public AudioOutputStatus Status => _context == 0 ? AudioOutputStatus.Lost : WebAudioBackend.Status(_context);

    public void Post(AudioCommand command)
    {
        switch (command)
        {
            case AudioCommand.Play play:
                if (_context == 0 || !backend.TryGetSound(play.Sound, out var sound))
                {
                    _ended.Enqueue(play.Voice);
                    return;
                }

                var handle = _nextHandle++;
                _voices[play.Voice] = handle;
                _voicesByHandle[handle] = play.Voice;
                var settings = play.Settings;
                _commands.AddRange([Play, handle, sound, GetBus(play.Bus).Handle, settings.Gain, settings.Pan, settings.Pitch, settings.Loop ? 1 : 0]);
                break;
            case AudioCommand.Release release when _voices.TryGetValue(release.Voice, out var voice):
                _commands.AddRange([Release, voice]);
                break;
            case AudioCommand.SetVoice set when _voices.TryGetValue(set.Voice, out var voice):
                _commands.AddRange([SetVoice, voice, (int)set.Parameter, set.Value, set.Ramp.TotalSeconds]);
                break;
            case AudioCommand.StopVoice stop when _voices.TryGetValue(stop.Voice, out var voice):
                _commands.AddRange([StopVoice, voice, stop.Fade.TotalSeconds]);
                break;
            case AudioCommand.SetBus set:
                var bus = GetBus(set.Bus);
                if (set.Parameter == BusParameter.Gain)
                {
                    bus.Gain = set.Value;
                }
                else
                {
                    bus.Cutoff = set.Value;
                }

                _commands.AddRange([SetBus, bus.Handle, (int)set.Parameter, set.Value, set.Ramp.TotalSeconds]);
                break;
            case AudioCommand.RemoveBus remove when _buses.TryGetValue(remove.Bus, out var removed):
                removed.IsRemoved = true;
                _commands.AddRange([RemoveBus, removed.Handle]);
                break;
        }
    }

    public void Flush()
    {
        if (_context == 0)
        {
            _commands.Clear();
            return;
        }

        var ended = WebAudioBackend.Submit(_context, [.. _commands]);
        _commands.Clear();
        foreach (var handle in ended)
        {
            if (_voicesByHandle.Remove(handle, out var voice))
            {
                _voices.Remove(voice);
                _ended.Enqueue(voice);
            }
        }
    }

    public bool TryTakeEnded(out VoiceId voice) => _ended.TryDequeue(out voice);

    public Result Reopen(string? deviceKey, AudioOutputSettings settings)
    {
        Close();
        if (WebAudioBackend.OpenContext(deviceKey, settings).TryPickProblems(out var problems, out var context))
        {
            return problems;
        }

        _context = context;
        foreach (var bus in _buses.Values.Where(bus => !bus.IsRemoved))
        {
            _commands.AddRange([SetBus, bus.Handle, (int)BusParameter.Gain, bus.Gain, 0]);
            if (bus.Cutoff is { } cutoff)
            {
                _commands.AddRange([SetBus, bus.Handle, (int)BusParameter.Cutoff, cutoff, 0]);
            }
        }

        return Result.Success();
    }

    public void Dispose() => Close();

    private void Close()
    {
        if (_context != 0)
        {
            WebAudioBackend.Close(_context);
            _context = 0;
        }

        foreach (var voice in _voices.Keys)
        {
            _ended.Enqueue(voice);
        }

        _voices.Clear();
        _voicesByHandle.Clear();
        _commands.Clear();
    }

    private Bus GetBus(BusId id)
    {
        if (!_buses.TryGetValue(id, out var bus))
        {
            _buses[id] = bus = new Bus(_nextHandle++);
        }

        bus.IsRemoved = false;
        return bus;
    }

    private sealed class Bus(int handle)
    {
        public int Handle { get; } = handle;
        public float Gain { get; set; } = 1;
        public float? Cutoff { get; set; }
        public bool IsRemoved { get; set; }
    }
}
