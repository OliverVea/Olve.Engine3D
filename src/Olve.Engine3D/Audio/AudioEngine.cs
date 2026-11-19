using System.Collections.Concurrent;
using Olve.Engine3D.Audio.Synthesis;

namespace Olve.Engine3D.Audio;

/// <summary>
/// One output's mix, on its audio thread: plays voices into their buses, mixes the buses, and limits the result.
/// The main thread reaches it only through the <see cref="AudioMixer"/>'s commands, applied at the start of each block,
/// and hears back which voices ended. A bus is created by the first command that names it.
/// </summary>
public sealed class AudioEngine : IAudioStream
{
    /// <summary>Peaks are held below this, so the output never clips.</summary>
    public const float LimiterThreshold = 0.95f;

    private static readonly TimeSpan LimiterRelease = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentQueue<(AudioCommand Command, BakedSound? Sound)> _commands = new();
    private readonly ConcurrentQueue<VoiceId> _ended = new();

    // Owned by the audio thread
    private readonly Dictionary<BusId, Bus> _buses = [];
    private readonly Dictionary<VoiceId, Voice> _voices = [];
    private readonly List<VoiceId> _endedThisBlock = [];
    private readonly HashSet<BusId> _removing = [];
    private double _limiterEnvelope;

    internal void Post(AudioCommand command, BakedSound? sound = null) => _commands.Enqueue((command, sound));

    internal bool TryTakeEnded(out VoiceId voice) => _ended.TryDequeue(out voice);

    public void Render(SampleBuffer buffer, int sampleRate)
    {
        while (_commands.TryDequeue(out var posted))
        {
            Apply(posted.Command, posted.Sound, sampleRate);
        }

        buffer.Reset(buffer.Channels, buffer.Frames);
        foreach (var bus in _buses.Values)
        {
            bus.Mix.Reset(buffer.Channels, buffer.Frames);
        }

        foreach (var (id, voice) in _voices)
        {
            if (!voice.Render(_buses[voice.Bus].Mix, sampleRate))
            {
                _endedThisBlock.Add(id);
            }
        }

        foreach (var id in _endedThisBlock)
        {
            _voices.Remove(id);
            _ended.Enqueue(id);
        }

        _endedThisBlock.Clear();

        foreach (var bus in _buses.Values)
        {
            bus.MixInto(buffer, sampleRate);
        }

        _removing.RemoveWhere(TryRemoveBus);
        Limit(buffer, sampleRate);
    }

    private bool TryRemoveBus(BusId bus)
    {
        foreach (var voice in _voices.Values)
        {
            if (voice.Bus == bus)
            {
                return false;
            }
        }

        _buses.Remove(bus);
        return true;
    }

    private Bus GetOrAddBus(BusId id)
    {
        _removing.Remove(id);
        if (!_buses.TryGetValue(id, out var bus))
        {
            _buses[id] = bus = new Bus();
        }

        return bus;
    }

    private void Apply(AudioCommand command, BakedSound? sound, int sampleRate)
    {
        switch (command)
        {
            case AudioCommand.RemoveBus remove when _buses.ContainsKey(remove.Bus):
                _removing.Add(remove.Bus);
                break;
            case AudioCommand.Play play when sound is null:
                _ended.Enqueue(play.Voice);
                break;
            case AudioCommand.Play play:
                GetOrAddBus(play.Bus);
                _voices[play.Voice] = new Voice(new BakedSource(sound, play.Settings.Loop), play.Bus, play.Settings);
                break;
            case AudioCommand.Release release when _voices.TryGetValue(release.Voice, out var voice):
                voice.Release();
                break;
            case AudioCommand.SetVoice set when _voices.TryGetValue(set.Voice, out var voice):
                voice.Set(set.Parameter, set.Value, Frames(set.Ramp, sampleRate));
                break;
            case AudioCommand.StopVoice stop when _voices.TryGetValue(stop.Voice, out var voice):
                voice.Stop(Frames(stop.Fade, sampleRate));
                break;
            case AudioCommand.SetBus set:
                GetOrAddBus(set.Bus).Set(set.Parameter, set.Value, Frames(set.Ramp, sampleRate));
                break;
        }
    }

    // Holds peaks below the threshold: the gain drops at once and recovers over the release
    private void Limit(SampleBuffer buffer, int sampleRate)
    {
        var release = double.Exp(-1 / (LimiterRelease.TotalSeconds * sampleRate));
        for (var frame = 0; frame < buffer.Frames; frame++)
        {
            var peak = 0.0;
            for (var channel = 0; channel < buffer.Channels; channel++)
            {
                peak = double.Max(peak, double.Abs(buffer.GetChannel(channel)[frame]));
            }

            _limiterEnvelope = double.Max(peak, _limiterEnvelope * release);
            var gain = _limiterEnvelope > LimiterThreshold ? LimiterThreshold / _limiterEnvelope : 1;

            for (var channel = 0; channel < buffer.Channels; channel++)
            {
                var samples = buffer.GetChannel(channel);
                samples[frame] = float.Clamp((float)(samples[frame] * gain), -1, 1);
            }
        }
    }

    private static int Frames(TimeSpan duration, int sampleRate) => (int)double.Round(duration.TotalSeconds * sampleRate);

    /// <summary>A value that moves to its target in a straight line, a frame at a time, so changes don't click.</summary>
    private struct Ramp(float value)
    {
        private float _step;
        private int _remaining;

        public float Value { get; private set; } = value;
        public float Target { get; private set; } = value;

        public void To(float target, int frames)
        {
            Target = target;
            if (frames <= 0)
            {
                Value = target;
                _remaining = 0;
                return;
            }

            _step = (target - Value) / frames;
            _remaining = frames;
        }

        public float Next()
        {
            if (_remaining > 0 && --_remaining == 0)
            {
                Value = Target;
            }
            else if (_remaining > 0)
            {
                Value += _step;
            }

            return Value;
        }
    }

    private sealed class Voice(VoiceSource source, BusId bus, VoiceSettings settings)
    {
        private Ramp _gain = new(settings.Gain);
        private Ramp _pan = new(settings.Pan);
        private Ramp _pitch = new(settings.Pitch);
        private double _position;
        private bool _stopping;

        public BusId Bus { get; } = bus;
        public VoiceSource Source { get; } = source;

        public void Release() => Source.Release((long)double.Floor(_position));

        public void Set(VoiceParameter parameter, float value, int frames)
        {
            switch (parameter)
            {
                case VoiceParameter.Gain when !_stopping:
                    _gain.To(value, frames);
                    break;
                case VoiceParameter.Pan:
                    _pan.To(float.Clamp(value, -1, 1), frames);
                    break;
                case VoiceParameter.Pitch:
                    _pitch.To(float.Max(value, 0), frames);
                    break;
            }
        }

        public void Stop(int fadeFrames)
        {
            _stopping = true;
            _gain.To(0, fadeFrames);
        }

        /// <summary>Adds the voice's next frames into the mix; false once it has ended.</summary>
        public bool Render(SampleBuffer mix, int sampleRate)
        {
            var rateRatio = (double)Source.SampleRate / sampleRate;

            for (var frame = 0; frame < mix.Frames; frame++)
            {
                if (_stopping && _gain.Value == 0 && _gain.Target == 0)
                {
                    return false;
                }

                if (Source.HasEnded(_position))
                {
                    return false;
                }

                var sample = Interpolate(Source, _position) * _gain.Next();
                var pan = _pan.Next();
                Write(mix, frame, sample, pan);

                _position = Source.Wrap(_position + _pitch.Next() * rateRatio);
            }

            return true;
        }

        // Equal-power pan of a mono sample onto the first two channels
        private static void Write(SampleBuffer mix, int frame, float sample, float pan)
        {
            if (mix.Channels == 1)
            {
                mix.GetChannel(0)[frame] += sample;
                return;
            }

            var angle = (pan + 1) * double.Pi / 4;
            mix.GetChannel(0)[frame] += (float)(sample * double.Cos(angle));
            mix.GetChannel(1)[frame] += (float)(sample * double.Sin(angle));
        }

        // Catmull-Rom between the samples around the position; also converts the source's rate to the output's
        private static float Interpolate(VoiceSource source, double position)
        {
            var index = (long)double.Floor(position);
            var t = (float)(position - index);

            var p0 = source.At(index - 1);
            var p1 = source.At(index);
            var p2 = source.At(index + 1);
            var p3 = source.At(index + 2);

            return p1 + 0.5f * t * (p2 - p0 + t * (2 * p0 - 5 * p1 + 4 * p2 - p3 + t * (3 * (p1 - p2) + p3 - p0)));
        }
    }

    private sealed class Bus
    {
        /// <summary>A cutoff at or above this leaves the bus unfiltered.</summary>
        private const float OpenCutoff = 20_000;

        // Butterworth: as flat as a lowpass gets, without a resonant peak
        private const double ButterworthQ = -3.0103;

        private Ramp _gain = new(1);
        private Ramp _cutoff = new(OpenCutoff);
        private Biquad[] _filters = [];
        private float _filterCutoff = float.NaN;

        public SampleBuffer Mix { get; } = new();

        public void Set(BusParameter parameter, float value, int frames)
        {
            switch (parameter)
            {
                case BusParameter.Gain:
                    _gain.To(float.Max(value, 0), frames);
                    break;
                case BusParameter.Cutoff:
                    _cutoff.To(float.Clamp(value, 10, OpenCutoff), frames);
                    break;
            }
        }

        public void MixInto(SampleBuffer output, int sampleRate)
        {
            if (_filters.Length != Mix.Channels)
            {
                _filters = new Biquad[Mix.Channels];
                _filterCutoff = float.NaN;
            }

            for (var frame = 0; frame < Mix.Frames; frame++)
            {
                var gain = _gain.Next();
                var cutoff = _cutoff.Next();
                var filtered = cutoff < OpenCutoff;
                if (filtered && cutoff != _filterCutoff)
                {
                    _filterCutoff = cutoff;
                    for (var channel = 0; channel < _filters.Length; channel++)
                    {
                        _filters[channel].SetCoefficients(FilterType.Lowpass, cutoff, ButterworthQ, 0, sampleRate);
                    }
                }

                for (var channel = 0; channel < Mix.Channels; channel++)
                {
                    var sample = Mix.GetChannel(channel)[frame];
                    if (filtered)
                    {
                        sample = (float)_filters[channel].Process(sample);
                    }

                    output.GetChannel(channel)[frame] += sample * gain;
                }
            }
        }
    }
}
