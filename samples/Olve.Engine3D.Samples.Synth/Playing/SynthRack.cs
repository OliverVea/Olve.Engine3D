using Microsoft.Extensions.Logging;
using Olve.Engine3D.Samples.Synth.Patching;

namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// The patch being played: every edit to it, and every note, goes through here and on to the audio, so what is heard
/// always matches the patch. The audio hears its cables.
/// </summary>
public sealed class SynthRack(ISynthAudio audio, ILogger<SynthRack> logger)
{
    public SynthPatch Patch { get; private set; } = new();

    public KeyVoices Notes { get; } = new();

    // The modules realised once per voice, and the cables the audio has
    private IReadOnlySet<ModuleId> _voiced = new HashSet<ModuleId>();
    private List<Cable> _heard = [];

    public ISynthAudio Audio => audio;

    /// <summary>Realises a patch on an empty rack.</summary>
    public Result Load(SynthPatch patch)
    {
        if (Patch.Modules.Count > 0)
        {
            return new ResultProblem("The rack already holds a patch");
        }

        Patch = patch;
        _voiced = Voicing.Voiced(patch);
        // The keys play as many voices as the patch's keyboard says, and the voiced modules are built for that many
        if (patch.Modules.FirstOrDefault(m => m.Kind == ModuleKind.Keyboard) is { } keyboard)
        {
            Notes.SetVoices((int)patch.GetValue(keyboard.Id, Modules.Voices));
        }

        foreach (var module in patch.Modules)
        {
            audio.AddModule(module.Id, module.Kind, VoicesOf(module.Id));
            foreach (var parameter in module.Definition.Parameters)
            {
                audio.SetParameter(module.Id, parameter.Name, patch.GetValue(module.Id, parameter.Name));
            }
        }

        _heard = [.. patch.Cables];
        foreach (var cable in _heard)
        {
            audio.Connect(cable);
        }

        logger.LogInformation("Loaded a patch of {Modules} modules and {Cables} cables", patch.Modules.Count, patch.Cables.Count);
        return Result.Success();
    }

    public Result<Cable> Connect(JackRef a, JackRef b)
    {
        if (Patch.Connect(a, b).TryPickProblems(out var problems, out var change))
        {
            return problems;
        }

        if (change.Replaced is { } replaced)
        {
            logger.LogInformation("Unplugged {Cable}", Describe(replaced));
        }

        Rehear();
        logger.LogInformation("Plugged {Cable}", Describe(change.Added));
        if (Patch.Find(change.Added.From.Module)?.Kind == ModuleKind.LineIn)
        {
            // Patching the line input is using it: only now does the page ask for an input
            audio.UseLineIn(change.Added.From.Module);
        }

        return change.Added;
    }

    public Result Disconnect(Cable cable)
    {
        var removed = Patch.Disconnect(cable);
        if (removed.TryPickProblems(out var problems))
        {
            return problems;
        }

        if (removed.WasNotFound)
        {
            return new ResultProblem("No cable {0} to unplug", Describe(cable));
        }

        Rehear();
        logger.LogInformation("Unplugged {Cable}", Describe(cable));
        return Result.Success();
    }

    public Result SetValue(ModuleId module, string parameter, float value)
    {
        var before = Patch.GetValue(module, parameter);
        if (Patch.SetValue(module, parameter, value).TryPickProblems(out var problems, out var set))
        {
            return problems;
        }

        if (set == before)
        {
            return Result.Success();
        }

        audio.SetParameter(module, parameter, set);
        logger.LogDebug("Set {Module} {Parameter} to {Value}", module, parameter, set);
        if (parameter == Modules.Voices)
        {
            foreach (var change in Notes.SetVoices((int)set))
            {
                Play(change);
            }

            // Each voiced module is built again for that many voices, its knobs and cables as they were, and the keys
            // still held sound again on them
            foreach (var voiced in _voiced)
            {
                audio.SetVoices(voiced, VoicesOf(voiced));
            }

            Replay();

            logger.LogInformation("The keys play {Voices} voices", (int)set);
        }

        if (parameter == Modules.Octave)
        {
            foreach (var (voice, semitone) in Notes.Playing)
            {
                audio.NoteOn(module, voice, Pitch(module, semitone), retrigger: false);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// A press on the line input's display (a step of 1) or its step buttons (1 or -1): starts it, or once it plays,
    /// changes to the next or the previous input.
    /// </summary>
    public void StepLineIn(ModuleId lineIn, int step = 1)
    {
        var status = audio.LineIn(lineIn);
        switch (status.State)
        {
            case LineInState.Asking:
            case LineInState.Live when status.Count < 2:
                break;
            case LineInState.Live:
                audio.StepLineIn(lineIn, step);
                logger.LogInformation("Changed the line input from {Device}", status.Device);
                break;
            default:
                audio.UseLineIn(lineIn);
                logger.LogInformation("Started the line input");
                break;
        }
    }

    public void Press(int semitone) => Play(Notes.Press(semitone));

    public void Release(int semitone) => Play(Notes.Release(semitone));

    public void ReleaseAll()
    {
        foreach (var change in Notes.ReleaseAll())
        {
            Play(change);
        }
    }

    /// <summary>The keyboard's pitch for a semitone from its C, in octaves from middle C.</summary>
    public float Pitch(ModuleId keyboard, int semitone) => Patch.GetValue(keyboard, Modules.Octave) + semitone / 12f;

    private void Play(VoiceChange? change)
    {
        foreach (var keyboard in Patch.Modules.Where(m => m.Kind == ModuleKind.Keyboard))
        {
            switch (change)
            {
                case VoiceChange.Play play:
                    audio.NoteOn(keyboard.Id, play.Voice, Pitch(keyboard.Id, play.Semitone), play.Retrigger);
                    break;
                case VoiceChange.Stop stop:
                    audio.NoteOff(keyboard.Id, stop.Voice);
                    break;
            }
        }
    }

    /// <summary>
    /// Whether a jack's cables carry the keys' voices, one signal each: it is an output of a module realised once per
    /// voice, that differs between them, while the keys play more than one.
    /// </summary>
    public bool IsPolyphonic(JackRef jack) =>
        Notes.Voices > 1 && _voiced.Contains(jack.Module) && Patch.FindJack(jack) is { Direction: JackDirection.Output, Shared: false };

    /// <summary>Whether a cable carries the keys' voices: whether its output does (<see cref="IsPolyphonic(JackRef)"/>).</summary>
    public bool IsPolyphonic(Cable cable) => IsPolyphonic(cable.From);

    // A voiced module is built for as many voices as the keys play, so no idle copies run; changing VOICES builds it again
    private int VoicesOf(ModuleId module) => _voiced.Contains(module) ? Notes.Voices : 1;

    // After a cable changes: unplugs from the audio what is no longer heard, rebuilds each module the change voiced or left
    // shared, then plugs in what is heard now, so no new connection reaches a module about to be rebuilt
    private void Rehear()
    {
        var heard = Patch.Cables.ToList();
        foreach (var gone in _heard.Except(heard))
        {
            audio.Disconnect(gone);
        }

        var rebuilt = Revoice();
        foreach (var added in heard.Except(_heard))
        {
            audio.Connect(added);
        }

        _heard = heard;
        if (rebuilt)
        {
            // A module built again starts silent, its envelopes closed: the keys still held open them again
            Replay();
        }
    }

    // Builds again each module the patch now voices or no longer does; whether there was any
    private bool Revoice()
    {
        var before = _voiced;
        _voiced = Voicing.Voiced(Patch);
        var rebuilt = false;
        foreach (var module in Patch.Modules.Where(m => before.Contains(m.Id) != _voiced.Contains(m.Id)))
        {
            audio.SetVoices(module.Id, VoicesOf(module.Id));
            logger.LogDebug("{Module} now plays on {Voices} voices", Title(module.Id), VoicesOf(module.Id));
            rebuilt = true;
        }

        return rebuilt;
    }

    // Starts every note the keys hold afresh, as on rebuilt voices
    private void Replay()
    {
        foreach (var (voice, semitone) in Notes.Playing)
        {
            Play(new VoiceChange.Play(voice, semitone, Retrigger: true));
        }
    }

    private string Describe(Cable cable) =>
        $"{Title(cable.From.Module)} {cable.From.Jack} -> {Title(cable.To.Module)} {cable.To.Jack}";

    private string Title(ModuleId module) => Patch.Find(module)?.Definition.Title ?? module.ToString();
}
