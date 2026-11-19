using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Engine3D.Input;
using Olve.Engine3D.Scenes;
using Olve.Results;

namespace Olve.Engine3D.Samples.Triangle;

public class ChimeService(
    SoundManager soundManager,
    AudioMixer mixer,
    AudioDeviceManager devices,
    AudioSegment segment,
    InputState input,
    ILogger<ChimeService> logger) : ISceneService
{
    private static readonly Patch Chime = new(
        [
            new PatchNode.Oscillator("tone", Waveform.Sine, 660),
            new PatchNode.Envelope("shape", TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(400), 0, TimeSpan.FromMilliseconds(50)),
            new PatchNode.Gain("volume", 0),
            new PatchNode.Gain("output", 0.2f),
        ],
        [
            new PatchConnection("tone", "volume"),
            new PatchConnection("shape", "volume", PatchNode.Gain.AmountParameter),
            new PatchConnection("volume", "output"),
        ],
        [],
        "output");

    private readonly HashSet<VoiceId> _chimes = [];
    private SoundId _sound;
    private BusId _bus;
    private VoiceSettings? _clicked;

    public Result Load()
    {
        if (soundManager.Register(Chime, TimeSpan.FromMilliseconds(400), segment).TryPickProblems(out var problems, out var sound))
        {
            return problems.Prepend("Failed to register the chime");
        }

        _sound = sound;
        _bus = mixer.AddBus(new BusName("chime"));
        return Result.Success();
    }

    public Result Input()
    {
        if (input.WasPressed(MouseButton.Left))
        {
            var position = input.GetNormalizedPosition(input.MainPointer);
            _clicked = new VoiceSettings(Pan: float.Clamp(position.X, -1, 1), Pitch: MathF.Pow(2, -float.Clamp(position.Y, -1, 1)));
        }

        return Result.Success();
    }

    public Result Update()
    {
        foreach (var ended in _chimes.Where(chime => mixer.Voices.All(voice => voice.Id != chime)).ToList())
        {
            _chimes.Remove(ended);
            logger.LogInformation("The chime ended");
        }

        if (_clicked is not { } settings || soundManager.IsRendering(_sound) || devices.Outputs.All(output => output.Status != AudioOutputStatus.Playing))
        {
            return Result.Success();
        }

        _clicked = null;
        if (mixer.Play(_sound, _bus, settings, segment).TryPickProblems(out var problems, out var chime))
        {
            return problems.Prepend("Failed to play the chime");
        }

        _chimes.Add(chime);
        logger.LogInformation("Chimed");
        return Result.Success();
    }
}
