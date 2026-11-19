using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Utilities;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio;

/// <summary>A quiet sine, looped: one second holds a whole number of periods, so the loop is seamless.</summary>
public sealed class TestTone(AudioMixer mixer, SoundManager soundManager)
{
    public const float Frequency = 440;
    public const float Amplitude = 0.1f;

    private static readonly Patch Patch = new(
        [new PatchNode.Oscillator("tone", Waveform.Sine, Frequency), new PatchNode.Gain("volume", Amplitude)],
        [new PatchConnection("tone", "volume")],
        [],
        "volume");

    // Lives as long as the app
    private readonly Id<AudioSegment> _segment = Id.New<AudioSegment>();
    private (SoundId Sound, BusId Bus)? _prepared;

    public VoiceId? Voice { get; private set; }

    public Result Start()
    {
        if (_prepared is null)
        {
            if (soundManager.Register(Patch, TimeSpan.FromSeconds(1), _segment).TryPickProblems(out var problems, out var sound))
            {
                return problems;
            }

            _prepared = (sound, mixer.AddBus(new BusName("test-tone")));
        }

        var (preparedSound, bus) = _prepared.Value;
        return mixer.Play(preparedSound, bus, new VoiceSettings(Loop: true), _segment)
            .IfSucceeded(voice => Voice = voice)
            .ToEmptyResult();
    }

    public void Stop()
    {
        if (Voice is { } voice)
        {
            mixer.Stop(voice, TimeSpan.FromMilliseconds(20));
            Voice = null;
        }
    }
}

public class PlayTestToneCommandHandler(TestTone testTone, ILogger<PlayTestToneCommandHandler> logger) : ICommandHandler
{
    public string Verb => "play-test-tone";
    public string HelpString => $"Plays a quiet {TestTone.Frequency} Hz test tone until stop-test-tone";
    public IReadOnlyList<CommandArgument> Arguments => [];

    public Result<CommandOutput> Handle(CommandContext commandContext)
    {
        if (testTone.Voice is not null)
        {
            return new CommandOutput("Test tone is already playing");
        }

        if (testTone.Start().TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to start the test tone");
        }

        logger.LogInformation("Test tone started");
        return new CommandOutput("Test tone started");
    }
}

public class StopTestToneCommandHandler(TestTone testTone, ILogger<StopTestToneCommandHandler> logger) : ICommandHandler
{
    public string Verb => "stop-test-tone";
    public string HelpString => "Stops the test tone";
    public IReadOnlyList<CommandArgument> Arguments => [];

    public Result<CommandOutput> Handle(CommandContext commandContext)
    {
        if (testTone.Voice is null)
        {
            return new CommandOutput("Test tone isn't playing");
        }

        testTone.Stop();
        logger.LogInformation("Test tone stopped");
        return new CommandOutput("Test tone stopped");
    }
}
