using System.Collections.Immutable;

namespace Olve.Engine3D.Samples.Synth.Patching;

public enum ModuleKind
{
    Keyboard,
    Oscillators,
    Lfo,
    Random,
    LineIn,
    Filter,
    Vca,
    Envelope,
    Drive,
    Phaser,
    Delay,
    Reverb,
    Mixer,
    Output,
    Slew,
    Atten,
    Sequencer,
    Quantizer,
    Lights,
    Scope,
}

public enum LightSide
{
    Left,
    Right,
}

public enum JackDirection
{
    Input,
    Output,
}

/// <summary>
/// What a jack carries, which its cables' colour shows. Audio is sound; a control voltage is a pitch in octaves, a level,
/// a sweep. A gate is a note held or not: it opens an envelope, and as a continuous signal it is 1 while a note is held,
/// else 0. Audio and control voltages are both continuous, so either goes into the other's inputs.
/// </summary>
public enum SignalKind
{
    Audio,
    Control,
    Gate,
}

/// <summary>A jack on a module. A knob's own CV input names the knob in <paramref name="Knob"/>; it sits under the knob.</summary>
public sealed record JackDefinition(string Name, JackDirection Direction, SignalKind Signal, string? Knob = null)
{
    public bool IsKnobInput => Knob is not null;

    /// <summary>An output the same on every voice of its module, such as the mod wheel's: its cables carry one signal.</summary>
    public bool Shared { get; init; }

    /// <summary>
    /// For an output that passes on what its module's input of this name hears, such as SLEW's: it carries audio when that
    /// does, else a control voltage (see <see cref="SynthPatch.SignalOf"/>).
    /// </summary>
    public string? Follows { get; init; }
}

public enum ParameterScale
{
    Linear,

    /// <summary>Equal ratios for equal turns, for frequencies and times. The minimum must be above 0.</summary>
    Exponential,

    /// <summary>Whole numbers from the minimum to the maximum; with choices, each number names one.</summary>
    Stepped,
}

public sealed record ParameterDefinition(
    string Name,
    float Min,
    float Max,
    float Default,
    ParameterScale Scale,
    string Unit = "",
    ImmutableArray<string> Choices = default,
    string? Label = null)
{
    public bool HasChoices => !Choices.IsDefaultOrEmpty;

    /// <summary>The name on the panel, short enough to fit under a knob.</summary>
    public string Caption => Label ?? Name.ToUpperInvariant();

    /// <summary>The name of the knob's CV input: a signal there turns the knob, as <see cref="Knobs.Modulate"/> says.</summary>
    public string CvJack => $"{Name} {Modules.Cv}";

    /// <summary>
    /// How far one unit of signal in the knob's CV input turns it, as a share of its sweep: <see cref="Knobs.CvTurn"/>
    /// unless the module says otherwise. At 0 the signal doesn't turn the knob; the module uses it another way.
    /// </summary>
    public float CvTurn { get; init; } = Knobs.CvTurn;
}

public sealed record ModuleDefinition(
    ModuleKind Kind,
    string Title,
    ImmutableArray<ParameterDefinition> Parameters,
    ImmutableArray<JackDefinition> Jacks)
{
    /// <summary>The name over its panel, which may use the dot font's · and differ from the title, such as "VCA · RING".</summary>
    public string Heading { get; init; } = Title;

    /// <summary>How its card looks on the rack, if not as <see cref="ModuleFaces.Default"/> lays it out.</summary>
    public ModuleFace? Face { get; init; }

    /// <summary>How many of its repeated parts it has: the bank's oscillators, the mixer's channels, the sequencer's steps.</summary>
    public int Parts { get; init; }

    public ParameterDefinition? FindParameter(string name) => Parameters.FirstOrDefault(p => p.Name == name);

    public JackDefinition? FindJack(string name) => Jacks.FirstOrDefault(j => j.Name == name);
}


/// <summary>
/// The modules a patch is built from: their knobs and jacks. Pitch is in octaves, 0 being middle C, so a pitch input
/// follows the keyboard one octave per octave; audio swings about ±1. Every knob has a CV input of its own besides the
/// jacks listed here. A module hears only what is cabled into it.
/// </summary>
public static class Modules
{
    public const string Out = "Out";
    public const string In = "In";
    public const string Gate = "Gate";
    public const string Pitch = "Pitch";
    public const string Cutoff = "Cutoff";
    public const string Cv = "CV";

    public const string Octave = "Octave";
    public const string Wave = "Wave";
    public const string Tune = "Tune";
    public const string Fine = "Fine";
    public const string Rate = "Rate";
    public const string Depth = "Depth";
    public const string Sync = "Sync";
    public const string Type = "Type";
    public const string Resonance = "Res";
    public const string Mod = "Mod";
    public const string Attack = "Attack";
    public const string Decay = "Decay";
    public const string Sustain = "Sustain";
    public const string Release = "Release";
    public const string Level = "Level";
    public const string Ring = "Ring";
    public const string Volume = "Volume";
    public const string Pan = "Pan";
    public const string InLeft = "In L";
    public const string InRight = "In R";
    public const string Amount = "Amount";
    public const string Fold = "Fold";
    public const string Tone = "Tone";
    public const string Mix = "Mix";
    public const string Time = "Time";
    public const string Feedback = "Feedback";
    public const string Size = "Size";
    public const string Gain = "Gain";
    public const string Env = "Env";
    public const string Audio = "Audio";
    public const string Noise = "Noise";
    public const string SampleHold = "S&H";
    public const string Smooth = "Smooth";
    public const string Rise = "Rise";
    public const string Fall = "Fall";
    public const string Shape = "Shape";
    public const string Scale = "Scale";
    public const string Offset = "Offset";
    public const string Clock = "Clock";
    public const string ClockIn = "Clock In";
    public const string Run = "Run";
    public const string Tempo = "Tempo";
    public const string Length = "Length";
    public const string Reset = "Reset";
    public const string Bend = "Bend";
    public const string Voices = "Voices";

    /// <summary>How far the pitch wheel bends the keyboard's pitch at either end, in semitones.</summary>
    public const float BendSemitones = 2;

    /// <summary>The oscillators in the OSCILLATORS bank.</summary>
    public const int Vcos = 3;

    public const int MixerChannels = 4;

    public const int SequencerSteps = 16;

    /// <summary>A part of one of the OSCILLATORS bank's oscillators, from 1: its knob or jack, "VCO 2 Tune" or "VCO 2 Out".</summary>
    public static string Vco(int vco, string part) => $"VCO {vco} {part}";

    /// <summary>The sequencer's knob for a step's pitch, from 1.</summary>
    public static string StepPitch(int step) => $"Step {step}";

    /// <summary>The sequencer's switch for whether a step opens the gate, from 1.</summary>
    public static string StepGate(int step) => $"Gate {step}";

    public const string Intensity = "Intensity";
    public const string Hue = "Hue";
    public const string Saturation = "Saturation";

    /// <summary>The lights module's knob for a part of the left or the right light: "Left Hue".</summary>
    public static string LightKnob(LightSide side, string part) => $"{side} {part}";

    /// <summary>The mixer's input for a channel, from 1.</summary>
    public static string MixerIn(int channel) => $"{In} {channel}";

    /// <summary>The mixer's knob for a channel's level, from 1.</summary>
    public static string MixerLevel(int channel) => $"{Level} {channel}";

    private static readonly ImmutableArray<string> Waves = ["SINE", "SQR", "SAW", "TRI"];

    private static JackDefinition InputJack(string name, SignalKind signal = SignalKind.Control) => new(name, JackDirection.Input, signal);

    private static JackDefinition OutputJack(string name, SignalKind signal = SignalKind.Control) => new(name, JackDirection.Output, signal);

    private const SignalKind Sound = SignalKind.Audio;

    // A module's jacks: the ones given, then a CV input for each knob
    private static ModuleDefinition Define(ModuleKind kind, string title, ImmutableArray<ParameterDefinition> parameters, ImmutableArray<JackDefinition> jacks) =>
        new(kind, title, parameters, [.. jacks, .. parameters.Select(p => new JackDefinition(p.CvJack, JackDirection.Input, SignalKind.Control, p.Name))]);

    // How much of the dry signal an effect replaces with its own
    private static ParameterDefinition MixKnob(float initial) => new(Mix, 0, 1, initial, ParameterScale.Linear);

    /// <summary>
    /// The keybed: each key held plays a voice of its own, as many at once as VOICES says (one plays the newest key alone),
    /// its PITCH and GATE outputs carrying every voice; the octave buttons shift them, the pitch wheel bends them up to
    /// <see cref="BendSemitones"/> either way and springs back, and the mod wheel sets the MOD output, one signal, from 0
    /// to 1. Its knobs are played by hand, so they have no CV inputs.
    /// </summary>
    public static readonly ModuleDefinition Keyboard = new(ModuleKind.Keyboard, "KEYS",
        [
            new ParameterDefinition(Octave, -3, 3, 0, ParameterScale.Stepped, Label: "OCT"),
            new ParameterDefinition(Bend, -BendSemitones, BendSemitones, 0, ParameterScale.Linear, "ST", Label: "PITCH"),
            new ParameterDefinition(Mod, 0, 1, 0, ParameterScale.Linear, Label: "MOD"),
            new ParameterDefinition(Voices, 1, MaxVoices, MaxVoices, ParameterScale.Stepped, Label: "VOICES"),
        ],
        [OutputJack(Pitch), OutputJack(Gate, SignalKind.Gate), OutputJack(Mod) with { Shared = true }]);

    /// <summary>The most voices the keys play at once.</summary>
    public const int MaxVoices = 6;

    /// <summary>
    /// Three oscillators, each following its own PITCH input. Each VCO's OUT is its wave at full level; MIX sums them, each
    /// at its LEVEL. Two saws a few cents apart and a square an octave down, to start with.
    /// </summary>
    public static readonly ModuleDefinition Oscillators = Define(ModuleKind.Oscillators, "OSCILLATORS",
        [
            .. VcoKnobs(1, wave: 2, tune: 0, fine: 0, level: 0.22f),
            .. VcoKnobs(2, wave: 2, tune: 0, fine: 7, level: 0.19f),
            .. VcoKnobs(3, wave: 1, tune: -12, fine: 0, level: 0.16f),
        ],
        [
            .. Enumerable.Range(1, Vcos).Select(v => InputJack(Vco(v, Pitch))),
            .. Enumerable.Range(1, Vcos).Select(v => OutputJack(Vco(v, Out), Sound)),
            OutputJack(Mix, Sound),
        ]) with
    {
        Parts = Vcos,
        // A strip for each VCO, its MIX out at the end of the last
        Face = new ModuleFace(ModuleGroup.Source, [.. Enumerable.Range(1, Vcos).Select(v => new FaceStrip($"VCO {v}", [Vco(v, Pitch)],
            [.. new[] { Wave, Tune, Fine, Level }.Select(part => (FaceControl)new FaceControl.Knob(Vco(v, part)))],
            [Vco(v, Out), v == Vcos ? Mix : ""]))]),
    };

    private static IEnumerable<ParameterDefinition> VcoKnobs(int vco, float wave, float tune, float fine, float level) =>
    [
        new ParameterDefinition(Vco(vco, Wave), 0, 3, wave, ParameterScale.Stepped, Choices: Waves, Label: "WAVE"),
        new ParameterDefinition(Vco(vco, Tune), -24, 24, tune, ParameterScale.Stepped, "ST", Label: "TUNE"),
        new ParameterDefinition(Vco(vco, Fine), -50, 50, fine, ParameterScale.Stepped, "CT", Label: "FINE"),
        new ParameterDefinition(Vco(vco, Level), 0, 1, level, ParameterScale.Linear, Label: "LEVEL"),
    ];

    /// <summary>A slow oscillator swinging DEPTH either way of 0. A gate into SYNC starts its wave again as it opens.</summary>
    public static readonly ModuleDefinition Lfo = Define(ModuleKind.Lfo, "LFO",
        [
            new ParameterDefinition(Wave, 0, 3, 0, ParameterScale.Stepped, Choices: Waves),
            new ParameterDefinition(Rate, 0.05f, 20, 0.4f, ParameterScale.Exponential, "HZ"),
            new ParameterDefinition(Depth, 0, 2, 0.8f, ParameterScale.Linear, "OCT"),
        ],
        [InputJack(Sync, SignalKind.Gate), OutputJack(Out)]);

    /// <summary>
    /// Chance: NOISE is white noise; S&amp;H a new random level between -1 and 1 each time the gate in CLOCK opens, or RATE
    /// times a second while nothing is plugged into CLOCK; SMOOTH glides from one of those levels to the next.
    /// </summary>
    public static readonly ModuleDefinition Random = Define(ModuleKind.Random, "RANDOM",
        [new ParameterDefinition(Rate, 0.1f, 30, 4, ParameterScale.Exponential, "HZ")],
        [InputJack(Clock, SignalKind.Gate), OutputJack(Noise, Sound), OutputJack(SampleHold), OutputJack(Smooth)]);

    /// <summary>
    /// An audio input of the device, such as a guitar through an audio interface, turned up by the gain (AUDIO), and its
    /// envelope (ENV): how loud it is, rising over the attack and falling over the release, for a pick to open a filter.
    /// </summary>
    public static readonly ModuleDefinition LineIn = Define(ModuleKind.LineIn, "LINE IN",
        [
            new ParameterDefinition(Gain, 0.1f, 20, 1, ParameterScale.Exponential, "X"),
            new ParameterDefinition(Attack, 0.001f, 0.5f, 0.01f, ParameterScale.Exponential, "S", Label: "ATK"),
            new ParameterDefinition(Release, 0.01f, 2, 0.2f, ParameterScale.Exponential, "S", Label: "REL"),
        ],
        [OutputJack(Env), OutputJack(Audio, Sound)]) with { Heading = "LINE IN · FOLLOW" };

    /// <summary>
    /// A resonant low- or high-pass. One unit of signal in CUTOFF's CV input moves the cutoff MOD octaves, so an envelope
    /// there opens the filter with each note.
    /// </summary>
    public static readonly ModuleDefinition Filter = Define(ModuleKind.Filter, "FILTER",
        [
            new ParameterDefinition(Type, 0, 1, 0, ParameterScale.Stepped, Choices: ["LP", "HP"], Label: "LP/HP"),
            new ParameterDefinition(Cutoff, 40, 16000, 900, ParameterScale.Exponential, "HZ") { CvTurn = 0 },
            new ParameterDefinition(Resonance, 0, 20, 6, ParameterScale.Linear, "DB"),
            new ParameterDefinition(Mod, 0, 4, 2, ParameterScale.Linear, "OCT"),
        ],
        [InputJack(In, Sound), OutputJack(Out, Sound)]) with
    {
        Face = new ModuleFace(ModuleGroup.Shape,
            [new FaceStrip(null, [In], [new FaceControl.Knob(Type), new FaceControl.Knob(Cutoff, Big: true), new FaceControl.Knob(Resonance), new FaceControl.Knob(Mod)], [Out])]),
    };

    public static readonly ModuleDefinition Envelope = Define(ModuleKind.Envelope, "ENV",
        [
            new ParameterDefinition(Attack, 0.002f, 2, 0.01f, ParameterScale.Exponential, "S", Label: "ATK"),
            new ParameterDefinition(Decay, 0.005f, 2, 0.3f, ParameterScale.Exponential, "S", Label: "DEC"),
            new ParameterDefinition(Sustain, 0, 1, 0.6f, ParameterScale.Linear, Label: "SUS"),
            new ParameterDefinition(Release, 0.005f, 4, 0.4f, ParameterScale.Exponential, "S", Label: "REL"),
        ],
        [InputJack(Gate, SignalKind.Gate), OutputJack(Out)]) with
    {
        Face = new ModuleFace(ModuleGroup.Shape,
            [new FaceStrip(null, [Gate], [.. new[] { Attack, Decay, Sustain, Release }.Select(p => (FaceControl)new FaceControl.Fader(p))], [Out])]),
    };

    /// <summary>
    /// IN times the level: the LEVEL knob plus whatever is in its CV input, one for one. With RING off the level stops at
    /// 0; turned up to RING, a level below 0 turns the signal over, so audio into LEVEL ring-modulates IN.
    /// </summary>
    public static readonly ModuleDefinition Vca = Define(ModuleKind.Vca, "VCA",
        [
            new ParameterDefinition(Level, 0, 1, 0, ParameterScale.Linear) { CvTurn = 1 },
            new ParameterDefinition(Ring, 0, 1, 0, ParameterScale.Linear),
        ],
        [InputJack(In, Sound), OutputJack(Out, Sound)]) with { Heading = "VCA · RING" };

    /// <summary>
    /// The amount pushes the signal into a curve: a soft clip that rounds its peaks off, or turned towards FOLD, a folder
    /// that turns them back on themselves for a brighter, glassier sound. The mix blends it with the dry signal; it starts at 0, so the drive is heard only once turned up.
    /// </summary>
    public static readonly ModuleDefinition DriveEffect = Define(ModuleKind.Drive, "DRIVE/FOLD",
        [
            new ParameterDefinition(Amount, 1, 40, 3, ParameterScale.Exponential, "X"),
            new ParameterDefinition(Fold, 0, 1, 0, ParameterScale.Linear),
            MixKnob(0),
        ],
        [InputJack(In, Sound), OutputJack(Out, Sound)]);

    /// <summary>
    /// A chain of allpass filters swept by its own LFO at the rate, the two sides in opposite phase, mixed with the dry
    /// signal for moving notches; the depth is how much of the swept copy is heard, and the feedback sharpens it. Stereo.
    /// </summary>
    public static readonly ModuleDefinition Phaser = Define(ModuleKind.Phaser, "PHASER",
        [
            new ParameterDefinition(Rate, 0.02f, 8, 0.3f, ParameterScale.Exponential, "HZ"),
            new ParameterDefinition(Depth, 0, 1, 0.4f, ParameterScale.Linear),
            new ParameterDefinition(Feedback, 0, 0.9f, 0.3f, ParameterScale.Linear, Label: "FDBK"),
        ],
        [InputJack(In, Sound), OutputJack(Out, Sound)]);

    /// <summary>
    /// A ping-pong echo: the signal on the left after the time, then on the right, each repeat fed back quieter by the
    /// feedback. The mod wobbles the time, so a short time with some mod is a chorus. Stereo.
    /// </summary>
    public static readonly ModuleDefinition Delay = Define(ModuleKind.Delay, "DELAY/CHO",
        [
            new ParameterDefinition(Time, 0.008f, 1.5f, 0.3f, ParameterScale.Exponential, "S"),
            new ParameterDefinition(Feedback, 0, 0.95f, 0.35f, ParameterScale.Linear, Label: "FDBK"),
            new ParameterDefinition(Mod, 0, 1, 0.1f, ParameterScale.Linear),
            MixKnob(0.25f),
        ],
        [InputJack(In, Sound), OutputJack(Out, Sound)]);

    /// <summary>A room with a stereo tail: the size blends a short tail into a long one, and the tone darkens it.</summary>
    public static readonly ModuleDefinition Reverb = Define(ModuleKind.Reverb, "REVERB",
        [
            new ParameterDefinition(Size, 0, 1, 0.5f, ParameterScale.Linear),
            new ParameterDefinition(Tone, 500, 16000, 5000, ParameterScale.Exponential, "HZ"),
            MixKnob(0.25f),
        ],
        [InputJack(In, Sound), OutputJack(Out, Sound)]);

    public static readonly ModuleDefinition Mixer = Define(ModuleKind.Mixer, "MIXER",
        [.. Enumerable.Range(1, MixerChannels).Select(c => new ParameterDefinition(MixerLevel(c), 0, 1, 0.5f, ParameterScale.Linear, Label: $"LVL {c}"))],
        [.. Enumerable.Range(1, MixerChannels).Select(c => InputJack(MixerIn(c), Sound)), OutputJack(Out, Sound)]) with
    {
        Parts = MixerChannels,
        Face = new ModuleFace(ModuleGroup.Output,
            [new FaceStrip(null, [.. Enumerable.Range(1, MixerChannels).Select(MixerIn)], [.. Enumerable.Range(1, MixerChannels).Select(c => (FaceControl)new FaceControl.Fader(MixerLevel(c)))], [Out])]),
    };

    /// <summary>
    /// The speakers: IN L is heard on the left and IN R on the right, each taking its own side of a stereo signal, through
    /// the volume and a limiter; IN R hears what IN L does while nothing is plugged into it. PAN turns one side down.
    /// </summary>
    public static readonly ModuleDefinition Output = Define(ModuleKind.Output, "OUT",
        [
            new ParameterDefinition(Volume, 0, 1, 0.5f, ParameterScale.Linear),
            new ParameterDefinition(Pan, -1, 1, 0, ParameterScale.Linear),
        ],
        [InputJack(InLeft, Sound), InputJack(InRight, Sound)]) with
    {
        Face = new ModuleFace(ModuleGroup.Output,
            [new FaceStrip(null, [InLeft, InRight], [new FaceControl.Knob(Volume, Big: true), new FaceControl.Knob(Pan)], [], FaceDisplay.LevelMeter)]),
    };

    /// <summary>
    /// Follows IN slowly: rising by one unit in the rise time and falling by one in the fall time, in a straight line
    /// (a glide), or turned towards SHAPE, closing in on the signal ever slower. Portamento on a pitch, or a smoothed S&amp;H.
    /// </summary>
    public static readonly ModuleDefinition Slew = Define(ModuleKind.Slew, "SLEW",
        [
            new ParameterDefinition(Rise, 0.001f, 5, 0.1f, ParameterScale.Exponential, "S"),
            new ParameterDefinition(Fall, 0.001f, 5, 0.1f, ParameterScale.Exponential, "S"),
            new ParameterDefinition(Shape, 0, 1, 0, ParameterScale.Linear),
        ],
        [InputJack(In), OutputJack(Out) with { Follows = In }]) with { Heading = "SLEW · GLIDE" };

    /// <summary>IN times the scale, from -1 (turned over) to 1, plus the offset: with nothing in IN, a steady signal.</summary>
    public static readonly ModuleDefinition Atten = Define(ModuleKind.Atten, "ATTEN",
        [
            new ParameterDefinition(Scale, -1, 1, 1, ParameterScale.Linear),
            new ParameterDefinition(Offset, -1, 1, 0, ParameterScale.Linear),
        ],
        [InputJack(In), OutputJack(Out) with { Follows = In }]);

    /// <summary>
    /// Sixteen steps, each a pitch within an octave either way of middle C and a gate, played in turn from step 1 while it
    /// runs, one step a sixteenth note at the tempo, back to step 1 after the length. Each step sets the pitch and, if its
    /// gate is on, opens the gate for half the step; the clock opens for half of every step. A gate plugged into the clock
    /// input moves it on a step each time it opens instead of the tempo, so it follows another sequencer or the keys, and a
    /// gate into RESET sends it back, so the next step it plays is step 1. It plays on one voice: a gate from it opens every
    /// voice of the modules it is plugged into. The steps' pitch knobs and gate buttons are set by hand, so they have no CV
    /// inputs; RUN, TEMPO and LENGTH have.
    /// </summary>
    public static readonly ModuleDefinition Sequencer = new ModuleDefinition(ModuleKind.Sequencer, "SEQ",
        [
            new ParameterDefinition(Run, 0, 1, 0, ParameterScale.Stepped, Choices: ["STOP", "RUN"]),
            new ParameterDefinition(Tempo, 30, 300, 120, ParameterScale.Exponential, "BPM"),
            new ParameterDefinition(Length, 1, SequencerSteps, SequencerSteps, ParameterScale.Stepped, Label: "LEN"),
            .. Enumerable.Range(1, SequencerSteps).Select(s => new ParameterDefinition(StepPitch(s), -12, 12, 0, ParameterScale.Stepped, "ST", Label: $"{s}")),
            .. Enumerable.Range(1, SequencerSteps).Select(s => new ParameterDefinition(StepGate(s), 0, 1, 1, ParameterScale.Stepped, Choices: ["OFF", "ON"], Label: $"G{s}")),
        ],
        [
            InputJack(ClockIn, SignalKind.Gate), InputJack(Reset, SignalKind.Gate),
            OutputJack(Pitch), OutputJack(Gate, SignalKind.Gate), OutputJack(Clock, SignalKind.Gate),
            .. new[] { Run, Tempo, Length }.Select(knob => new JackDefinition($"{knob} {Cv}", JackDirection.Input, SignalKind.Control, knob)),
        ]) with
    {
        Heading = "SEQUENCER",
        Parts = SequencerSteps,
        // The steps' pitch knobs in a row between the clock input and the pitch and gate outputs, their gate buttons under
        // them with the light that runs along the steps, and the transport under those between RESET and the clock out,
        // so no two jacks' names share a row's slots
        Face = new ModuleFace(ModuleGroup.Source,
        [
            new FaceStrip(null, [ClockIn], [.. Enumerable.Range(1, SequencerSteps).Select(s => (FaceControl)new FaceControl.Knob(StepPitch(s)))], [Pitch, Gate]),
            new FaceStrip(null, [""], [.. Enumerable.Range(1, SequencerSteps).Select(s => (FaceControl)new FaceControl.Toggle(StepGate(s)))], ["", ""],
                FaceDisplay.StepLights),
            new FaceStrip(null, [Reset], [new FaceControl.Toggle(Run), new FaceControl.Knob(Tempo, Big: true), new FaceControl.Knob(Length)], [Clock]),
        ]),
    };

    /// <summary>The quantizer's scales, by their names on its readout: each the semitones above C it keeps.</summary>
    public static readonly ImmutableArray<(string Name, ImmutableArray<int> Semitones)> Scales =
    [
        ("CHROMATIC", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
        ("MAJOR", [0, 2, 4, 5, 7, 9, 11]),
        ("MINOR", [0, 2, 3, 5, 7, 8, 10]),
        ("PENTATONIC", [0, 2, 4, 7, 9]),
        ("BLUES", [0, 3, 5, 6, 7, 10]),
    ];

    /// <summary>
    /// Pulls a pitch in IN to the nearest note of the scale, in C, and sends it out of OUT: a sequencer's or RANDOM's
    /// wandering pitch made a tune. The scale is a switch set by hand, shown on its readout, so it has no CV input.
    /// </summary>
    public static readonly ModuleDefinition Quantizer = new ModuleDefinition(ModuleKind.Quantizer, "QUANTIZER",
        [new ParameterDefinition(Scale, 0, 4, 1, ParameterScale.Stepped, Choices: [.. Scales.Select(s => s.Name[..int.Min(5, s.Name.Length)])])],
        [InputJack(In), OutputJack(Out)]) with
    {
        Face = new ModuleFace(ModuleGroup.Utility,
        [
            new FaceStrip(null, [In], [new FaceControl.Knob(Scale)], [Out]),
            new FaceStrip(null, [], [], [], FaceDisplay.ScaleReadout),
        ]),
    };

    /// <summary>
    /// The room's two lights, left and right: how bright each is, and its colour's hue and saturation, each from 0 to 1.
    /// They start where the room's lights rest; a signal in a knob's CV input moves the light with it.
    /// </summary>
    public static readonly ModuleDefinition Lights = Define(ModuleKind.Lights, "LIGHTS",
        [
            .. LightKnobs(LightSide.Left, 0.9f, 0.08f, 0.35f),
            .. LightKnobs(LightSide.Right, 0.75f, 0.58f, 0.2f),
        ],
        []);

    private static IEnumerable<ParameterDefinition> LightKnobs(LightSide side, float intensity, float hue, float saturation)
    {
        return
        [
            new ParameterDefinition(LightKnob(side, Intensity), 0, 1, intensity, ParameterScale.Linear, Label: "INT"),
            new ParameterDefinition(LightKnob(side, Hue), 0, 1, hue, ParameterScale.Linear, Label: "HUE"),
            new ParameterDefinition(LightKnob(side, Saturation), 0, 1, saturation, ParameterScale.Linear, Label: "SAT"),
        ];
    }

    /// <summary>
    /// A CRT oscilloscope over a corner of the case: it shows the signal in its input as a green trace. It has no panel in
    /// the rack; its input jack sits under its screen.
    /// </summary>
    public static readonly ModuleDefinition Scope = Define(ModuleKind.Scope, "SCOPE", [], [InputJack(In)]);

    public static ModuleDefinition Of(ModuleKind kind) => kind switch
    {
        ModuleKind.Keyboard => Keyboard,
        ModuleKind.Oscillators => Oscillators,
        ModuleKind.Lfo => Lfo,
        ModuleKind.Random => Random,
        ModuleKind.LineIn => LineIn,
        ModuleKind.Filter => Filter,
        ModuleKind.Vca => Vca,
        ModuleKind.Envelope => Envelope,
        ModuleKind.Drive => DriveEffect,
        ModuleKind.Phaser => Phaser,
        ModuleKind.Delay => Delay,
        ModuleKind.Reverb => Reverb,
        ModuleKind.Mixer => Mixer,
        ModuleKind.Output => Output,
        ModuleKind.Slew => Slew,
        ModuleKind.Atten => Atten,
        ModuleKind.Sequencer => Sequencer,
        ModuleKind.Quantizer => Quantizer,
        ModuleKind.Lights => Lights,
        ModuleKind.Scope => Scope,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
