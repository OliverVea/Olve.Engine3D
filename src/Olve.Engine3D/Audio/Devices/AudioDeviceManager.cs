using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Audio.Devices;

/// <summary>
/// Opens outputs on the audio devices, any number at once. Each output plays its own voices and buses. Main thread
/// only; lives as long as the app.
/// </summary>
public sealed class AudioDeviceManager(IAudioDeviceBackend backend, ILogger<AudioDeviceManager> logger) : IDisposable
{
    private readonly Dictionary<AudioOutputId, Output> _outputs = [];
    private long _openedCount;

    public IReadOnlyList<AudioDeviceInfo> Devices => backend.GetDevices();

    /// <summary>The outputs, in the order they were opened.</summary>
    public IEnumerable<AudioOutputState> Outputs => _outputs.Values.OrderBy(output => output.Sequence).Select(GetState);

    /// <summary>The first output opened that's still open: where buses play unless routed elsewhere.</summary>
    public AudioOutputId? DefaultOutput => _outputs.Values.MinBy(output => output.Sequence)?.Id;

    public Result<AudioOutputId> Open(AudioDeviceSelection device, AudioOutputSettings settings)
    {
        if (Validate(settings).TryPickProblems(out var problems))
        {
            return problems;
        }

        var id = AudioOutputId.New();
        logger.LogDebug("Opening audio output {OutputId} on {Device}", id, device);
        IAudioOutput? opened = null;
        if (OpenFirst(device, key =>
            {
                if (backend.Open(key, settings).TryPickProblems(out var openProblems, out var output))
                {
                    return openProblems;
                }

                opened = output;
                return Result.Success();
            }).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to open an audio output");
        }

        var added = new Output(id, _openedCount++, opened!) { Selection = device, Settings = settings };
        _outputs[id] = added;
        logger.LogInformation("Opened audio output {OutputId} on '{DeviceKey}' ({SampleRate} Hz, {Channels} channels)",
            id, added.Audio.DeviceKey, added.Audio.Format?.SampleRate, added.Audio.Format?.Channels);
        return id;
    }

    /// <summary>
    /// Moves the output to another device or settings; on desktop its voices carry on. If that fails, the output goes
    /// back to what it was, or is lost if that fails too.
    /// </summary>
    public Result Reconfigure(AudioOutputId id, AudioDeviceSelection device, AudioOutputSettings settings)
    {
        if (!_outputs.TryGetValue(id, out var output))
        {
            return new ResultProblem("No audio output {0}", id);
        }

        if (Validate(settings).TryPickProblems(out var problems))
        {
            return problems;
        }

        logger.LogDebug("Reconfiguring audio output {OutputId} to {Device}", id, device);
        if (!OpenFirst(device, key => output.Audio.Reopen(key, settings)).TryPickProblems(out problems))
        {
            (output.Selection, output.Settings) = (device, settings);
            logger.LogInformation("Reconfigured audio output {OutputId} to '{DeviceKey}' ({SampleRate} Hz, {Channels} channels)",
                id, output.Audio.DeviceKey, output.Audio.Format?.SampleRate, output.Audio.Format?.Channels);
            return Result.Success();
        }

        if (OpenFirst(output.Selection, key => output.Audio.Reopen(key, output.Settings)).TryPickProblems(out var restoreProblems))
        {
            logger.LogWarning("Audio output {OutputId} is lost: it couldn't go back to its device either:{NewLine}{Problems}",
                id, Environment.NewLine, string.Join(Environment.NewLine, restoreProblems.Select(p => p.ToDebugString())));
        }

        return problems.Prepend("Failed to reconfigure the audio output; it's back on its previous device");
    }

    public Result Close(AudioOutputId id)
    {
        if (!_outputs.Remove(id, out var output))
        {
            return new ResultProblem("No audio output {0}", id);
        }

        output.Audio.Dispose();
        logger.LogInformation("Closed audio output {OutputId}", id);
        return Result.Success();
    }

    public Result<AudioOutputState> GetState(AudioOutputId id) =>
        _outputs.TryGetValue(id, out var output) ? GetState(output) : new ResultProblem("No audio output {0}", id);

    internal bool TryGetOutput(AudioOutputId id, out IAudioOutput output)
    {
        output = _outputs.TryGetValue(id, out var opened) ? opened.Audio : null!;
        return output is not null;
    }

    internal IEnumerable<IAudioOutput> OpenOutputs => _outputs.Values.Select(output => output.Audio);

    public void Dispose()
    {
        foreach (var output in _outputs.Values)
        {
            output.Audio.Dispose();
        }

        _outputs.Clear();
    }

    private static AudioOutputState GetState(Output output) => new(
        output.Id,
        output.Selection,
        output.Settings,
        output.Audio.DeviceKey,
        output.Audio.Format,
        output.Audio.Status);

    private static Result Validate(AudioOutputSettings settings)
    {
        if (settings.Channels is not (1 or 2))
        {
            return new ResultProblem("An output has 1 or 2 channels, not {0}", settings.Channels);
        }

        if (settings.SampleRate is <= 0)
        {
            return new ResultProblem("A sample rate is positive, not {0}", settings.SampleRate);
        }

        return settings.TargetLatency < TimeSpan.Zero
            ? new ResultProblem("A latency can't be negative")
            : Result.Success();
    }

    // Tries the selected device, then each fallback in turn
    private Result OpenFirst(AudioDeviceSelection device, Func<string?, Result> open)
    {
        var devices = backend.GetDevices();
        List<ResultProblem> problems = [];
        for (var selection = device; selection is not null;)
        {
            string? key;
            switch (selection)
            {
                case AudioDeviceSelection.Specific specific:
                    key = specific.Key;
                    selection = specific.Fallback;
                    if (devices.All(info => info.Key != key))
                    {
                        problems.Add(new ResultProblem("No audio device '{0}'", key));
                        continue;
                    }

                    break;
                default:
                    key = null;
                    selection = null;
                    break;
            }

            if (open(key).TryPickProblems(out var openProblems))
            {
                problems.AddRange(openProblems.Prepend($"Failed to open audio device '{key ?? "default"}'"));
                continue;
            }

            return Result.Success();
        }

        return new ResultProblemCollection(problems);
    }

    private sealed class Output(AudioOutputId id, long sequence, IAudioOutput audio)
    {
        public AudioOutputId Id { get; } = id;
        public long Sequence { get; } = sequence;
        public IAudioOutput Audio { get; } = audio;
        public required AudioDeviceSelection Selection { get; set; }
        public required AudioOutputSettings Settings { get; set; }
    }
}
