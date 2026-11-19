using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Devices;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio;

/// <summary>
/// Plays sounds as voices mixed into buses, each bus on an output: the one it's routed to, or the
/// <see cref="AudioDeviceManager.DefaultOutput"/>. Main thread only: every call is an <see cref="AudioCommand"/> to the
/// output, which plays it on its own, so nothing is shared. With no output open, nothing plays and voices end at once.
/// </summary>
public sealed class AudioMixer(AudioDeviceManager devices, SoundManager soundManager, ILogger<AudioMixer> logger)
{
    /// <summary>How long voices take to fade out when their segment is released or their bus moves.</summary>
    public static readonly TimeSpan SegmentFade = TimeSpan.FromMilliseconds(20);

    private readonly Dictionary<BusId, BusState> _buses = [];
    private readonly Dictionary<BusId, AudioOutputId> _placed = [];
    private readonly Dictionary<BusName, AudioOutputId> _routesByName = [];
    private readonly Dictionary<VoiceId, VoiceState> _voices = [];
    private long _playedCount;

    public IEnumerable<BusState> Buses => _buses.Values;

    /// <summary>The voices playing, in the order they started.</summary>
    public IEnumerable<VoiceState> Voices => _voices.Values.OrderBy(voice => voice.Sequence);

    public BusId AddBus(BusName name)
    {
        var id = BusId.New();
        _buses[id] = new BusState(id, name, 1, null, _routesByName.TryGetValue(name, out var output) ? output : null);
        logger.LogDebug("Added audio bus '{Bus}' ({BusId})", name, id);
        return id;
    }

    public Result<BusId> FindBus(BusName name)
    {
        var buses = _buses.Values.Where(bus => bus.Name == name).Take(2).ToList();
        return buses.Count switch
        {
            0 => new ResultProblem("There's no audio bus '{0}'", name),
            1 => buses[0].Id,
            _ => new ResultProblem("There are several audio buses named '{0}'", name),
        };
    }

    /// <summary>
    /// Plays the bus on the output from now on. Its voices fade out on the output it was on; while the output is
    /// closed, the bus plays on the default output.
    /// </summary>
    public Result Route(BusId bus, AudioOutputId output)
    {
        if (!_buses.TryGetValue(bus, out var state))
        {
            return new ResultProblem("No audio bus {0}", bus);
        }

        if (devices.GetState(output).TryPickProblems(out var problems))
        {
            return problems;
        }

        _buses[bus] = state with { Route = output };
        Place(bus);
        logger.LogDebug("Routed audio bus '{Bus}' to output {OutputId}", state.Name, output);
        return Result.Success();
    }

    /// <summary>Routes the buses with the name, and those added with it later, to the output.</summary>
    internal Result RouteByName(BusName busName, AudioOutputId output)
    {
        _routesByName[busName] = output;
        return Result.Concat([.. _buses.Values.Where(bus => bus.Name == busName).ToList().Select(bus => Route(bus.Id, output))]);
    }

    public Result<VoiceId> Play(SoundId sound, BusId bus, VoiceSettings settings, Id<AudioSegment> segment)
    {
        if (soundManager.CheckReady(sound).TryPickProblems(out var problems))
        {
            return problems;
        }

        if (!_buses.ContainsKey(bus))
        {
            return new ResultProblem("No audio bus {0}", bus);
        }

        var id = VoiceId.New();
        if (Place(bus) is not { } output || !devices.TryGetOutput(output, out var audio))
        {
            return id;
        }

        _voices[id] = new VoiceState(id, sound, bus, settings, segment, output, _playedCount++);
        audio.Post(new AudioCommand.Play(id, sound, bus, settings));
        logger.LogDebug("Playing sound {SoundId} as voice {VoiceId}", sound, id);
        return id;
    }

    /// <summary>Lets a looping voice play to the end of its sound and stop. A voice that has ended is left alone.</summary>
    public void Release(VoiceId voice)
    {
        if (_voices.TryGetValue(voice, out var state))
        {
            Post(state.Output, new AudioCommand.Release(voice));
        }
    }

    /// <summary>Moves a voice's parameter to the value over the ramp. A voice that has ended is left alone.</summary>
    public void Set(VoiceId voice, VoiceParameter parameter, float value, TimeSpan ramp)
    {
        if (!_voices.TryGetValue(voice, out var state))
        {
            return;
        }

        var settings = parameter switch
        {
            VoiceParameter.Gain => state.Settings with { Gain = value },
            VoiceParameter.Pan => state.Settings with { Pan = value },
            _ => state.Settings with { Pitch = value },
        };
        _voices[voice] = state with { Settings = settings };
        Post(state.Output, new AudioCommand.SetVoice(voice, parameter, value, ramp));
    }

    /// <summary>Fades a voice out; it ends when the fade does. A voice that has ended is left alone.</summary>
    public void Stop(VoiceId voice, TimeSpan fade)
    {
        if (_voices.TryGetValue(voice, out var state))
        {
            Post(state.Output, new AudioCommand.StopVoice(voice, fade));
        }
    }

    public void Set(BusId bus, BusParameter parameter, float value, TimeSpan ramp)
    {
        if (!_buses.TryGetValue(bus, out var state))
        {
            return;
        }

        _buses[bus] = parameter == BusParameter.Gain ? state with { Gain = value } : state with { Cutoff = value };
        if (Place(bus) is { } output)
        {
            Post(output, new AudioCommand.SetBus(bus, parameter, value, ramp));
        }
    }

    /// <summary>Fades out the voices the segment played.</summary>
    public void StopSegment(Id<AudioSegment> segment)
    {
        foreach (var voice in _voices.Values.Where(voice => voice.Segment == segment))
        {
            Post(voice.Output, new AudioCommand.StopVoice(voice.Id, SegmentFade));
        }
    }

    /// <summary>
    /// Forgets the voices that ended, and those of outputs that closed, moves buses whose output changed, and sends the
    /// frame's commands to the outputs. Called once a frame.
    /// </summary>
    public void Update()
    {
        foreach (var output in devices.OpenOutputs)
        {
            while (output.TryTakeEnded(out var voice))
            {
                _voices.Remove(voice);
            }
        }

        foreach (var voice in _voices.Values.Where(voice => !devices.TryGetOutput(voice.Output, out _)).ToList())
        {
            _voices.Remove(voice.Id);
        }

        foreach (var bus in _buses.Keys)
        {
            Place(bus);
        }

        foreach (var output in devices.OpenOutputs)
        {
            output.Flush();
        }
    }

    // Puts the bus on the output it should play on, moving it off the one it was on; returns that output
    private AudioOutputId? Place(BusId bus)
    {
        var state = _buses[bus];
        AudioOutputId? target = state.Route is { } route && devices.TryGetOutput(route, out _) ? route : devices.DefaultOutput;
        var current = _placed.TryGetValue(bus, out var placed) ? placed : (AudioOutputId?)null;
        if (current == target)
        {
            return target;
        }

        if (current is { } previous)
        {
            foreach (var voice in _voices.Values.Where(voice => voice.Bus == bus))
            {
                Post(previous, new AudioCommand.StopVoice(voice.Id, SegmentFade));
            }

            Post(previous, new AudioCommand.RemoveBus(bus));
            _placed.Remove(bus);
        }

        if (target is not { } next)
        {
            return null;
        }

        _placed[bus] = next;
        Post(next, new AudioCommand.SetBus(bus, BusParameter.Gain, state.Gain, TimeSpan.Zero));
        if (state.Cutoff is { } cutoff)
        {
            Post(next, new AudioCommand.SetBus(bus, BusParameter.Cutoff, cutoff, TimeSpan.Zero));
        }

        return next;
    }

    private void Post(AudioOutputId output, AudioCommand command)
    {
        if (devices.TryGetOutput(output, out var audio))
        {
            audio.Post(command);
        }
    }

    /// <param name="Cutoff">The lowpass cutoff last set, in Hz; null while the bus is unfiltered.</param>
    /// <param name="Route">The output the bus is routed to; null for the default output.</param>
    public sealed record BusState(BusId Id, BusName Name, float Gain, float? Cutoff, AudioOutputId? Route);

    /// <param name="Output">The output the voice plays on.</param>
    public sealed record VoiceState(
        VoiceId Id,
        SoundId Sound,
        BusId Bus,
        VoiceSettings Settings,
        Id<AudioSegment> Segment,
        AudioOutputId Output,
        long Sequence);
}
