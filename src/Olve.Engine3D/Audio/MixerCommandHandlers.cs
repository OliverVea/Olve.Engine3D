using System.Globalization;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Commands;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio;

internal static class MixerArguments
{
    public static readonly CommandArgument Ramp = new("ramp", "Milliseconds to move over (default 0)");

    public static Result<float?> ParseFloat(CommandContext context, CommandArgument argument)
    {
        var text = context.GetOptionalArgument(argument);
        if (text is null)
        {
            return (float?)null;
        }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : new ResultProblem("'{0}' isn't a number for {1}", text, argument.Name);
    }

    public static Result<TimeSpan> ParseMilliseconds(CommandContext context, CommandArgument argument)
    {
        if (ParseFloat(context, argument).TryPickProblems(out var problems, out var value))
        {
            return problems;
        }

        return value is null or >= 0
            ? TimeSpan.FromMilliseconds(value ?? 0)
            : new ResultProblem("{0} can't be negative", argument.Name);
    }

    public static Result<VoiceId> ParseVoice(CommandContext context, CommandArgument argument)
    {
        if (context.GetRequiredArgument(argument).TryPickProblems(out var problems, out var text))
        {
            return problems;
        }

        return Id.TryParse(text, out var id)
            ? new VoiceId(id)
            : new ResultProblem("'{0}' isn't a voice id", text);
    }
}

public class SetAudioBusCommandHandler(AudioMixer mixer, ILogger<SetAudioBusCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument NameArgument = new("name", "The bus's name", true);
    private static readonly CommandArgument GainArgument = new("gain", "Linear volume");
    private static readonly CommandArgument CutoffArgument = new("cutoff", "Lowpass cutoff in Hz; 20000 lets everything through");

    public string Verb => "set-audio-bus";
    public string HelpString => "Moves a bus's gain or lowpass cutoff to a value over a ramp";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [NameArgument, GainArgument, CutoffArgument, MixerArguments.Ramp];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (context.GetRequiredArgument(NameArgument).TryPickProblems(out var problems, out var name)
            || MixerArguments.ParseFloat(context, GainArgument).TryPickProblems(out problems, out var gain)
            || MixerArguments.ParseFloat(context, CutoffArgument).TryPickProblems(out problems, out var cutoff)
            || MixerArguments.ParseMilliseconds(context, MixerArguments.Ramp).TryPickProblems(out problems, out var ramp))
        {
            return problems;
        }

        if (mixer.Buses.FirstOrDefault(bus => bus.Name == new BusName(name)) is not { } bus)
        {
            return new ResultProblem("No audio bus '{0}'", name);
        }

        if (gain is { } gainValue)
        {
            mixer.Set(bus.Id, BusParameter.Gain, gainValue, ramp);
        }

        if (cutoff is { } cutoffValue)
        {
            mixer.Set(bus.Id, BusParameter.Cutoff, cutoffValue, ramp);
        }

        logger.LogInformation("Set audio bus '{Bus}' (gain {Gain}, cutoff {Cutoff}) over {Ramp} ms", name, gain, cutoff, ramp.TotalMilliseconds);
        return new CommandOutput($"Set audio bus '{name}'");
    }
}

public class SetVoiceCommandHandler(AudioMixer mixer, ILogger<SetVoiceCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument VoiceArgument = new("voice", "The voice's id", true);
    private static readonly CommandArgument GainArgument = new("gain", "Linear volume");
    private static readonly CommandArgument PanArgument = new("pan", "From -1 (left) to 1 (right)");
    private static readonly CommandArgument PitchArgument = new("pitch", "Playback rate; 2 is an octave up");

    public string Verb => "set-voice";
    public string HelpString => "Moves a playing voice's gain, pan or pitch to a value over a ramp";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [VoiceArgument, GainArgument, PanArgument, PitchArgument, MixerArguments.Ramp];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (MixerArguments.ParseVoice(context, VoiceArgument).TryPickProblems(out var problems, out var voice)
            || MixerArguments.ParseFloat(context, GainArgument).TryPickProblems(out problems, out var gain)
            || MixerArguments.ParseFloat(context, PanArgument).TryPickProblems(out problems, out var pan)
            || MixerArguments.ParseFloat(context, PitchArgument).TryPickProblems(out problems, out var pitch)
            || MixerArguments.ParseMilliseconds(context, MixerArguments.Ramp).TryPickProblems(out problems, out var ramp))
        {
            return problems;
        }

        if (mixer.Voices.All(state => state.Id != voice))
        {
            return new ResultProblem("Voice {0} isn't playing", voice);
        }

        foreach (var (parameter, value) in new[] { (VoiceParameter.Gain, gain), (VoiceParameter.Pan, pan), (VoiceParameter.Pitch, pitch) })
        {
            if (value is { } set)
            {
                mixer.Set(voice, parameter, set, ramp);
            }
        }

        logger.LogInformation("Set voice {VoiceId} (gain {Gain}, pan {Pan}, pitch {Pitch}) over {Ramp} ms", voice, gain, pan, pitch, ramp.TotalMilliseconds);
        return new CommandOutput($"Set voice {voice}");
    }
}

public class StopVoiceCommandHandler(AudioMixer mixer, ILogger<StopVoiceCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument VoiceArgument = new("voice", "The voice's id", true);
    private static readonly CommandArgument FadeArgument = new("fade", "Milliseconds to fade out over (default 0)");

    public string Verb => "stop-voice";
    public string HelpString => "Fades a playing voice out and ends it";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [VoiceArgument, FadeArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (MixerArguments.ParseVoice(context, VoiceArgument).TryPickProblems(out var problems, out var voice)
            || MixerArguments.ParseMilliseconds(context, FadeArgument).TryPickProblems(out problems, out var fade))
        {
            return problems;
        }

        if (mixer.Voices.All(state => state.Id != voice))
        {
            return new ResultProblem("Voice {0} isn't playing", voice);
        }

        mixer.Stop(voice, fade);
        logger.LogInformation("Stopped voice {VoiceId} over {Fade} ms", voice, fade.TotalMilliseconds);
        return new CommandOutput($"Stopped voice {voice}");
    }
}

public class ReleaseVoiceCommandHandler(AudioMixer mixer, ILogger<ReleaseVoiceCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument VoiceArgument = new("voice", "The voice's id", true);

    public string Verb => "release-voice";
    public string HelpString => "Lets a looping voice play to the end of its sound and stop";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [VoiceArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (MixerArguments.ParseVoice(context, VoiceArgument).TryPickProblems(out var problems, out var voice))
        {
            return problems;
        }

        if (mixer.Voices.All(state => state.Id != voice))
        {
            return new ResultProblem("Voice {0} isn't playing", voice);
        }

        mixer.Release(voice);
        logger.LogInformation("Released voice {VoiceId}", voice);
        return new CommandOutput($"Released voice {voice}");
    }
}
