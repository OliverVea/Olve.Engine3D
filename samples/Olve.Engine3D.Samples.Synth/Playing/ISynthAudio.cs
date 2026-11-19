using Olve.Engine3D.Samples.Synth.Patching;

namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// Where the patch is heard: each module realised as live audio nodes, once per voice if it is voiced (see
/// <see cref="Voicing"/>), each cable a connection between them. Pitch is in octaves from middle C.
/// </summary>
public interface ISynthAudio
{
    /// <summary>
    /// Reads back what the audio reports, once a frame before anything asks (<see cref="AudioFrameService"/>): whether it
    /// runs, its rate, the output's level, the metered peaks and the knobs read before. Those readbacks answer from it
    /// until the next refresh. An audio that answers each one as it is asked has nothing to do.
    /// </summary>
    void Refresh()
    {
    }

    /// <summary>Whether sound comes out. Browsers start audio only after the first click or key press.</summary>
    bool IsRunning { get; }

    /// <summary>The output's recent level, both speakers together, 0 for silence and about 1 at full scale.</summary>
    float OutputLevel { get; }

    /// <summary>
    /// Fills <paramref name="peaks"/> with the left and then the right speaker's peak magnitude over about the last frame
    /// and returns how many it filled: none while the audio isn't running.
    /// </summary>
    int ReadSpeakers(Span<float> peaks);

    /// <summary>The sequencer's step being heard now, from 0, or null while it is stopped.</summary>
    int? SequencerStep(ModuleId sequencer);

    /// <summary>Samples per second of the audio.</summary>
    float SampleRate { get; }

    /// <summary>
    /// Fills <paramref name="samples"/> with the latest stretch of the scope's input, oldest first, and returns how many it
    /// filled: none while the audio isn't running.
    /// </summary>
    int ReadScope(ModuleId scope, Span<float> samples);

    /// <summary>A knob's value as the audio has it now, with whatever a signal in its CV input adds; its first voice's.</summary>
    float ReadKnob(ModuleId module, string parameter);

    /// <summary>From now on, meters these outputs, every voice of each together, for <see cref="ReadLevels"/>.</summary>
    void MeterOutputs(IReadOnlyList<JackRef> outputs);

    /// <summary>
    /// Fills <paramref name="peaks"/> with each metered output's peak magnitude over about the last frame, in the order
    /// they were metered, and returns how many it filled: none while the audio isn't running.
    /// </summary>
    int ReadLevels(Span<float> peaks);

    void AddModule(ModuleId module, ModuleKind kind, int voices);

    /// <summary>Builds the module again with this many voices, its knobs and cables as they were.</summary>
    void SetVoices(ModuleId module, int voices);

    void SetParameter(ModuleId module, string parameter, float value);

    void Connect(Cable cable);

    void Disconnect(Cable cable);

    /// <summary>Opens the voice's gate at the pitch; with retrigger, the envelopes on that voice start again.</summary>
    void NoteOn(ModuleId keyboard, int voice, float pitch, bool retrigger);

    void NoteOff(ModuleId keyboard, int voice);

    LineInStatus LineIn(ModuleId lineIn);

    /// <summary>Starts the line input on the input chosen last time, asking the user for it first; again after a refusal.</summary>
    void UseLineIn(ModuleId lineIn);

    /// <summary>Changes the line input to the device's next input (a step of 1) or its previous one (-1), round from the last to the first.</summary>
    void StepLineIn(ModuleId lineIn, int step);
}
