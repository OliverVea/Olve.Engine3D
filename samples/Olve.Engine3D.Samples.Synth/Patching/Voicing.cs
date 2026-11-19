namespace Olve.Engine3D.Samples.Synth.Patching;

/// <summary>
/// Which modules play each note on a voice of their own, so a chord is several copies of the voice path. A keyboard has a
/// voice per note, and so has every oscillator bank, mixer, LFO, filter, envelope, VCA, slew and attenuator its cables
/// reach through modules of those kinds, except from an output shared by every voice, such as the mod
/// wheel's. The rest is shared: a module the notes don't reach hears every voice's copy of a signal into it summed,
/// and sends its own signal into every copy. The effects, the output, the scope, the line input, RANDOM and the sequencer
/// are always shared. So a sequencer plays one line: an envelope its gate opens is shared, and opens on every voice it feeds,
/// so a sequenced voice path takes its pitch from the sequencer too, not from the keys.
/// </summary>
public static class Voicing
{
    public static bool CanBeVoiced(ModuleKind kind) =>
        kind is ModuleKind.Keyboard or ModuleKind.Oscillators or ModuleKind.Mixer or ModuleKind.Lfo or ModuleKind.Filter
            or ModuleKind.Envelope or ModuleKind.Vca or ModuleKind.Slew or ModuleKind.Atten;

    public static IReadOnlySet<ModuleId> Voiced(SynthPatch patch)
    {
        var voiced = new HashSet<ModuleId>();
        var connections = patch.Cables;
        var pending = new Stack<ModuleId>(patch.Modules.Where(m => m.Kind == ModuleKind.Keyboard).Select(m => m.Id));
        while (pending.TryPop(out var module))
        {
            if (!voiced.Add(module))
            {
                continue;
            }

            foreach (var cable in connections.Where(c => c.From.Module == module && patch.FindJack(c.From) is { Shared: false }))
            {
                if (patch.Find(cable.To.Module) is { } to && CanBeVoiced(to.Kind))
                {
                    pending.Push(to.Id);
                }
            }
        }

        return voiced;
    }
}
