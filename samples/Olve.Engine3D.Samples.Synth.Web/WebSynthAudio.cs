using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;

namespace Olve.Engine3D.Samples.Synth.Web;

/// <summary>
/// The patch heard through the browser's Web Audio: <c>synth.js</c> builds native audio nodes for each module. Each knob is
/// sent as its turn, with the table <see cref="Knobs.Curve"/> to look its value up in, so a signal in its CV input turns it
/// in the audio just as <see cref="Knobs.Modulate"/> says.
/// </summary>
public sealed partial class WebSynthAudio : ISynthAudio
{
    public const string ModuleName = "Olve.Engine3D.Samples.Synth.Web.Synth";

    private readonly Dictionary<ModuleId, ModuleDefinition> _modules = [];

    // What the browser read back at the last refresh (readFrame in synth.js), and how many outputs it meters
    private readonly AudioFrame _frame = new();
    private int _metered;

    // The knobs read back at each refresh, in order: each one the first time it is asked for
    private readonly List<(ModuleId Module, string Parameter)> _knobs = [];
    private readonly Dictionary<(ModuleId, string), int> _knobSlots = [];
    private bool _knobsChanged;

    // The sequencers whose step is read back at each refresh, in order: each the first time it is asked for
    private readonly List<ModuleId> _steppers = [];
    private bool _steppersChanged;

    public void Refresh()
    {
        if (_knobsChanged)
        {
            WatchKnobs([.. _knobs.Select(k => k.Module.Value)], [.. _knobs.Select(k => k.Parameter)]);
            _knobsChanged = false;
        }

        if (_steppersChanged)
        {
            WatchSteps([.. _steppers.Select(s => s.Value)]);
            _steppersChanged = false;
        }

        ReadFrame(_frame.Into(_metered, _knobs.Count, _steppers.Count));
        _frame.Read(_knobs.Count, _steppers.Count);
    }

    public bool IsRunning => _frame.IsRead ? _frame.IsRunning : State() == "running";

    public float OutputLevel => _frame.IsRead ? _frame.OutputLevel : (float)Level();

    public float SampleRate => _frame.IsRead ? _frame.SampleRate : (float)GetSampleRate();

    public int ReadSpeakers(Span<float> peaks) => _frame.ReadSpeakers(peaks);

    public int? SequencerStep(ModuleId sequencer)
    {
        var slot = _steppers.IndexOf(sequencer);
        if (slot < 0)
        {
            _steppers.Add(sequencer);
            _steppersChanged = true;
        }
        else if (_frame.TryReadStep(slot, out var read))
        {
            return read;
        }

        return SequencerStep(sequencer.Value) is var step and >= 0 ? step : null;
    }

    public float ReadKnob(ModuleId module, string parameter)
    {
        if (_knobSlots.TryGetValue((module, parameter), out var slot))
        {
            if (_frame.TryReadKnob(slot, out var read))
            {
                return read;
            }
        }
        else
        {
            _knobSlots[(module, parameter)] = _knobs.Count;
            _knobs.Add((module, parameter));
            _knobsChanged = true;
        }

        return (float)ReadKnob(module.Value, parameter);
    }

    // The samples cross as the floats' bytes, written straight into the span
    public int ReadScope(ModuleId scope, Span<float> samples) => ReadScope(scope.Value, MemoryMarshal.AsBytes(samples));

    public void MeterOutputs(IReadOnlyList<JackRef> outputs)
    {
        MeterOutputs([.. outputs.Select(o => o.Module.Value)], [.. outputs.Select(o => o.Jack)]);
        // The peaks read back are for the outputs metered before, until the next refresh
        _metered = outputs.Count;
        _frame.Remeter();
    }

    public int ReadLevels(Span<float> peaks) => _frame.ReadLevels(peaks);

    public void AddModule(ModuleId module, ModuleKind kind, int voices)
    {
        var definition = Modules.Of(kind);
        _modules[module] = definition;
        // The module's jacks and how many channels, oscillators or steps it has, so synth.js builds it from the facts here
        AddModule(module.Value, kind.ToString(), voices,
            [.. definition.Jacks.Where(j => j.Direction == JackDirection.Input).Select(j => j.Name)],
            [.. definition.Jacks.Where(j => j.Direction == JackDirection.Output).Select(j => j.Name)],
            [.. definition.Jacks.Where(j => j.Shared).Select(j => j.Name)],
            definition.Parts);
        foreach (var parameter in definition.Parameters)
        {
            // A knob played only by hand, such as a wheel, has no CV input
            var cvJack = definition.FindJack(parameter.CvJack) is null ? "" : parameter.CvJack;
            DefineKnob(module.Value, parameter.Name, cvJack, Knobs.CvGain(parameter), [.. Knobs.Curve(parameter).Select(v => (double)v)]);
        }

        if (kind == ModuleKind.Quantizer)
        {
            // Its scales' tables, one after another, for its scale knob to choose from
            DefineScales(module.Value, Modules.Scales.Length, [.. Enumerable.Range(0, Modules.Scales.Length).SelectMany(Quantizing.Table).Select(v => (double)v)]);
        }

        // The browser checks it realises every jack the module has, so no cable reaches a jack with no audio behind it
        CheckModule(module.Value);
    }

    public void SetParameter(ModuleId module, string parameter, float value)
    {
        if (_modules.GetValueOrDefault(module)?.FindParameter(parameter) is { } definition)
        {
            SetParameter(module.Value, parameter, Knobs.ToTurn(definition, value), value);
        }
    }

    public void SetVoices(ModuleId module, int voices) => SetVoices(module.Value, voices);

    public void Connect(Cable cable) => Connect(cable.From.Module.Value, cable.From.Jack, cable.To.Module.Value, cable.To.Jack);

    public void Disconnect(Cable cable) => Disconnect(cable.From.Module.Value, cable.From.Jack, cable.To.Module.Value, cable.To.Jack);

    public void NoteOn(ModuleId keyboard, int voice, float pitch, bool retrigger) => NoteOn(keyboard.Value, voice, pitch, retrigger);

    public void NoteOff(ModuleId keyboard, int voice) => NoteOff(keyboard.Value, voice);

    public LineInStatus LineIn(ModuleId lineIn)
    {
        var status = LineIn(lineIn.Value);
        var state = status[0] switch
        {
            "asking" => LineInState.Asking,
            "live" => LineInState.Live,
            "denied" => LineInState.Denied,
            "none" => LineInState.NoInput,
            _ => LineInState.Idle,
        };
        return new LineInStatus(state, status[1], int.Parse(status[2], CultureInfo.InvariantCulture), int.Parse(status[3], CultureInfo.InvariantCulture));
    }

    public void UseLineIn(ModuleId lineIn) => UseLineIn(lineIn.Value);

    public void StepLineIn(ModuleId lineIn, int step) => StepLineIn(lineIn.Value, step);

    /// <summary>Whether the page is on a narrow or touch screen.</summary>
    [JSImport("narrowScreen", ModuleName)]
    public static partial bool NarrowScreen();

    /// <summary>Which parts of the room's finish the page wants: the bloom, the vignette and the colour fringe.</summary>
    public static FinishOptions FinishOptions() => FinishParts() is var parts
        ? new FinishOptions((parts & 1) != 0, (parts & 2) != 0, (parts & 4) != 0)
        : new FinishOptions();

    // A bit each: the bloom, the vignette, the fringe
    [JSImport("finishOptions", ModuleName)]
    private static partial int FinishParts();

    /// <summary>How many samples a pixel the page wants the room drawn with: 4, or 1 for <c>?msaa=0</c>.</summary>
    [JSImport("msaaSamples", ModuleName)]
    public static partial int Samples();

    [JSImport("state", ModuleName)]
    private static partial string State();

    [JSImport("level", ModuleName)]
    private static partial double Level();

    [JSImport("sequencerStep", ModuleName)]
    private static partial int SequencerStep(int sequencer);

    [JSImport("sampleRate", ModuleName)]
    private static partial double GetSampleRate();

    [JSImport("readScope", ModuleName)]
    private static partial int ReadScope(int scope, [JSMarshalAs<JSType.MemoryView>] Span<byte> samples);

    [JSImport("readFrame", ModuleName)]
    private static partial void ReadFrame([JSMarshalAs<JSType.MemoryView>] Span<double> frame);

    [JSImport("watchKnobs", ModuleName)]
    private static partial void WatchKnobs([JSMarshalAs<JSType.Array<JSType.Number>>] int[] modules, string[] parameters);

    [JSImport("readKnob", ModuleName)]
    private static partial double ReadKnob(int module, string parameter);

    [JSImport("meterOutputs", ModuleName)]
    private static partial void MeterOutputs([JSMarshalAs<JSType.Array<JSType.Number>>] int[] modules, string[] jacks);

    [JSImport("addModule", ModuleName)]
    private static partial void AddModule(int module, string kind, int voices, string[] inputs, string[] outputs, string[] shared, int count);

    [JSImport("setVoices", ModuleName)]
    private static partial void SetVoices(int module, int voices);

    [JSImport("checkModule", ModuleName)]
    private static partial void CheckModule(int module);

    [JSImport("defineScales", ModuleName)]
    private static partial void DefineScales(int module, int count, [JSMarshalAs<JSType.Array<JSType.Number>>] double[] tables);

    [JSImport("watchSteps", ModuleName)]
    private static partial void WatchSteps([JSMarshalAs<JSType.Array<JSType.Number>>] int[] sequencers);


    [JSImport("defineKnob", ModuleName)]
    private static partial void DefineKnob(int module, string parameter, string cvJack, double cvGain,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] curve);

    [JSImport("setParameter", ModuleName)]
    private static partial void SetParameter(int module, string parameter, double turn, double value);

    [JSImport("connect", ModuleName)]
    private static partial void Connect(int fromModule, string fromJack, int toModule, string toJack);

    [JSImport("disconnect", ModuleName)]
    private static partial void Disconnect(int fromModule, string fromJack, int toModule, string toJack);

    [JSImport("noteOn", ModuleName)]
    private static partial void NoteOn(int keyboard, int voice, double pitch, bool retrigger);

    [JSImport("noteOff", ModuleName)]
    private static partial void NoteOff(int keyboard, int voice);

    // The state, the input's label, its index and how many inputs there are
    [JSImport("lineIn", ModuleName)]
    [return: JSMarshalAs<JSType.Array<JSType.String>>]
    private static partial string[] LineIn(int lineIn);

    [JSImport("useLineIn", ModuleName)]
    private static partial void UseLineIn(int lineIn);

    [JSImport("stepLineIn", ModuleName)]
    private static partial void StepLineIn(int lineIn, int step);
}
