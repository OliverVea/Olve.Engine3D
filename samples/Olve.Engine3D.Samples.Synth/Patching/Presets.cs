namespace Olve.Engine3D.Samples.Synth.Patching;

public static class Presets
{
    /// <summary>
    /// Panel v2, read along the path of the sound: the oscillators, two LFOs, RANDOM and LINE IN; the filter, the VCA and
    /// two envelopes; the effects; the mixer, the output, SLEW and two ATTENs. Every connection is a cable: the keys play
    /// the three oscillators, two saws a few cents apart and a square an octave down, and open both envelopes; the first
    /// opens the VCA and the second sweeps the filter, and the voice goes on through the effects to both of the output's
    /// inputs. The left scope shows the voice before the effects, the right the first LFO, and the second LFO slowly moves
    /// the phaser's depth. The line input waits unpatched, so the page asks for a microphone only once it is used; the
    /// lights module sets the room's lights by hand until something is patched into it. In the side case the sequencer
    /// waits stopped with a tune set on its steps, its pitch cabled into the quantizer, for whoever patches them into the
    /// voice; the keys stay the voice's until then.
    /// </summary>
    /// <remarks>The modules after the scopes come last, so the ones before keep their ids.</remarks>
    public static SynthPatch Classic()
    {
        var patch = new SynthPatch();
        var keys = patch.Add(ModuleKind.Keyboard).Id;
        var oscillators = patch.Add(ModuleKind.Oscillators).Id;
        ModuleId[] lfos = [patch.Add(ModuleKind.Lfo).Id, patch.Add(ModuleKind.Lfo).Id];
        patch.Add(ModuleKind.Random);
        patch.Add(ModuleKind.LineIn);
        var filter = patch.Add(ModuleKind.Filter).Id;
        var vca = patch.Add(ModuleKind.Vca).Id;
        ModuleId[] envelopes = [patch.Add(ModuleKind.Envelope).Id, patch.Add(ModuleKind.Envelope).Id];
        var drive = patch.Add(ModuleKind.Drive).Id;
        var phaser = patch.Add(ModuleKind.Phaser).Id;
        var delay = patch.Add(ModuleKind.Delay).Id;
        var reverb = patch.Add(ModuleKind.Reverb).Id;
        patch.Add(ModuleKind.Mixer);
        var output = patch.Add(ModuleKind.Output).Id;
        patch.Add(ModuleKind.Slew);
        patch.Add(ModuleKind.Atten);
        patch.Add(ModuleKind.Atten);
        patch.Add(ModuleKind.Lights);
        ModuleId[] scopes = [patch.Add(ModuleKind.Scope).Id, patch.Add(ModuleKind.Scope).Id];
        var sequencer = patch.Add(ModuleKind.Sequencer).Id;
        var quantizer = patch.Add(ModuleKind.Quantizer).Id;

        Check(patch.SetValue(lfos[1], Modules.Rate, 0.15f));
        for (var step = 1; step <= Modules.SequencerSteps; step++)
        {
            Check(patch.SetValue(sequencer, Modules.StepPitch(step), Tune[step - 1]));
            Check(patch.SetValue(sequencer, Modules.StepGate(step), Rests.Contains(step) ? 0 : 1));
        }

        (JackRef From, JackRef To)[] cables =
        [
            // The keys play the oscillators and open the envelopes
            .. Enumerable.Range(1, Modules.Vcos).Select(vco => (new JackRef(keys, Modules.Pitch), new JackRef(oscillators, Modules.Vco(vco, Modules.Pitch)))),
            .. envelopes.Select(envelope => (new JackRef(keys, Modules.Gate), new JackRef(envelope, Modules.Gate))),
            // The voice, from the oscillators through the filter, the VCA and the effects to both sides of the output
            (new(oscillators, Modules.Mix), new(filter, Modules.In)),
            (new(filter, Modules.Out), new(vca, Modules.In)),
            (new(vca, Modules.Out), new(drive, Modules.In)),
            (new(drive, Modules.Out), new(phaser, Modules.In)),
            (new(phaser, Modules.Out), new(delay, Modules.In)),
            (new(delay, Modules.Out), new(reverb, Modules.In)),
            (new(reverb, Modules.Out), new(output, Modules.InLeft)),
            (new(reverb, Modules.Out), new(output, Modules.InRight)),
            // The first envelope opens the VCA and the second sweeps the filter
            (new(envelopes[0], Modules.Out), new(vca, Modules.Vca.FindParameter(Modules.Level)!.CvJack)),
            (new(envelopes[1], Modules.Out), new(filter, Modules.Filter.FindParameter(Modules.Cutoff)!.CvJack)),
            // For show: the scopes and the phaser's depth
            (new(vca, Modules.Out), new(scopes[0], Modules.In)),
            (new(lfos[0], Modules.Out), new(scopes[1], Modules.In)),
            (new(lfos[1], Modules.Out), new(phaser, Modules.Phaser.FindParameter(Modules.Depth)!.CvJack)),
            // In the side case, the sequencer's pitch through the quantizer
            (new(sequencer, Modules.Pitch), new(quantizer, Modules.In)),
        ];
        foreach (var (from, to) in cables)
        {
            Check(patch.Connect(from, to));
        }

        return patch;
    }

    // The sequencer's tune, in semitones from middle C, and the steps that rest
    private static readonly int[] Tune = [0, 0, 12, 0, 7, 0, 10, 12, -5, -5, 7, -5, 3, 5, 7, 10];
    private static readonly int[] Rests = [4, 8, 12];

    private static void Check<T>(Result<T> result)
    {
        if (result.TryPickProblems(out var problems))
        {
            throw new InvalidOperationException(string.Join("; ", problems.Select(p => p.ToBriefString())));
        }
    }
}
