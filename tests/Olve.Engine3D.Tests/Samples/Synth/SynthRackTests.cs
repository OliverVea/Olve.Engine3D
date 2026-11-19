using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class SynthRackTests
{
    private static readonly ModuleId Keyboard = Preset.Keyboard, Lfo = Preset.Lfo, Filter = Preset.Filter, Envelope = Preset.Envelope;

    private readonly RecordingAudio _audio = new();
    private readonly SynthRack _rack;

    public SynthRackTests()
    {
        _rack = new SynthRack(_audio, NullLogger<SynthRack>.Instance);
        if (_rack.Load(Presets.Classic()).TryPickProblems(out _))
        {
            throw new InvalidOperationException("The preset didn't load");
        }
    }

    [Test]
    public async Task Loading_realises_every_module_knob_and_cable()
    {
        var patch = _rack.Patch;

        await Assert.That(_audio.Calls.Count(c => c.StartsWith("add "))).IsEqualTo(patch.Modules.Count);
        await Assert.That(_audio.Calls.Count(c => c.StartsWith("set "))).IsEqualTo(patch.Modules.Sum(m => m.Definition.Parameters.Length));
        await Assert.That(_audio.Calls.Where(c => c.StartsWith("connect "))).IsEquivalentTo(
            [.. patch.Cables.Select(c => $"connect {c}")]);
        await Assert.That(_rack.Load(Presets.Classic())).Failed();
    }

    [Test]
    public async Task Repatching_an_input_unplugs_what_it_heard_from_the_audio_before_plugging_the_new_and_unplugging_leaves_it_hearing_nothing()
    {
        var cutoff = Preset.CvOf(Filter, Modules.Cutoff);
        var preset = new Cable(new JackRef(Preset.Envelope2, Modules.Out), cutoff);
        var lfo = new Cable(new JackRef(Lfo, Modules.Out), cutoff);
        var envelope = new Cable(new JackRef(Envelope, Modules.Out), cutoff);
        _audio.Calls.Clear();

        await Assert.That(_rack.Connect(lfo.From, lfo.To)).Succeeded();
        await Assert.That(_rack.Connect(envelope.From, envelope.To)).Succeeded();
        await Assert.That(_rack.Disconnect(envelope)).Succeeded();

        await Assert.That(_audio.Log).IsEqualTo(Lines(
            $"disconnect {preset}", $"connect {lfo}",
            $"disconnect {lfo}", $"connect {envelope}",
            $"disconnect {envelope}"));
        await Assert.That(_rack.Patch.CableInto(cutoff)).IsNull();
    }

    [Test]
    public async Task A_refused_cable_and_an_unchanged_knob_send_nothing()
    {
        _audio.Calls.Clear();

        await Assert.That(_rack.Connect(new JackRef(Lfo, Modules.Out), new JackRef(Envelope, Modules.Gate))).Failed();
        await Assert.That(_rack.SetValue(Filter, Modules.Cutoff, _rack.Patch.GetValue(Filter, Modules.Cutoff))).Succeeded();
        await Assert.That(_audio.Calls).IsEmpty();
    }

    [Test]
    public async Task Held_keys_play_a_chord_and_an_octave_change_moves_every_held_note_without_retriggering()
    {
        _audio.Calls.Clear();

        _rack.Press(0); // C
        _rack.Press(4); // E
        _rack.Press(7); // G
        _rack.Release(4);
        await Assert.That(_rack.SetValue(Keyboard, Modules.Octave, 1)).Succeeded(); // up an octave while C and G sound
        _rack.ReleaseAll();

        await Assert.That(_audio.Log).IsEqualTo(Lines(
            "note-on 1/0 0 retrigger",
            "note-on 1/1 0.333 retrigger",
            "note-on 1/2 0.583 retrigger",
            "note-off 1/1",
            "set 1 Octave 1",
            "note-on 1/0 1 legato",
            "note-on 1/2 1.583 legato",
            "note-off 1/0",
            "note-off 1/2"));
    }

    [Test]
    public async Task The_preset_plays_the_voice_path_its_cables_reach_once_per_voice_and_shares_the_rest()
    {
        var voiced = _rack.Patch.Modules.Where(m => _audio.Calls.Contains($"add {m.Id} {m.Kind} x6")).Select(m => m.Kind);

        await Assert.That(voiced).IsEquivalentTo(
            [ModuleKind.Keyboard, ModuleKind.Oscillators, ModuleKind.Filter, ModuleKind.Vca, ModuleKind.Envelope, ModuleKind.Envelope]);
        await Assert.That(_audio.Calls.Count(c => c.StartsWith("add ") && c.EndsWith(" x1")))
            .IsEqualTo(_rack.Patch.Modules.Count - voiced.Count());
    }

    [Test]
    public async Task Patching_the_keyboard_into_the_LFO_gives_it_a_voice_per_note_before_the_cable_reaches_it_and_unplugging_shares_it_again()
    {
        var rate = new JackRef(Lfo, Modules.Lfo.FindParameter(Modules.Rate)!.CvJack);
        var cable = new Cable(new JackRef(Keyboard, Modules.Pitch), rate);
        _rack.Press(0); // held throughout: each rebuild starts it again
        _audio.Calls.Clear();

        await Assert.That(_rack.Connect(cable.From, cable.To)).Succeeded();
        await Assert.That(_rack.Disconnect(cable)).Succeeded();

        await Assert.That(_audio.Log).IsEqualTo(Lines(
            $"voices {Lfo} 6", $"connect {cable}", "note-on 1/0 0 retrigger",
            $"disconnect {cable}", $"voices {Lfo} 1", "note-on 1/0 0 retrigger"));
    }

    [Test]
    public async Task Changing_VOICES_builds_the_voiced_modules_again_for_that_many_voices_and_the_shared_ones_not_and_the_held_key_sounds_again()
    {
        _rack.Press(0); // C
        _rack.Press(4); // E, the newer
        _audio.Calls.Clear();

        await Assert.That(_rack.SetValue(Keyboard, Modules.Voices, 1)).Succeeded();
        var built = _audio.Calls.Where(c => c.StartsWith("voices ")).ToList();
        await Assert.That(_audio.Calls.Where(c => !c.StartsWith("voices "))).IsEquivalentTo(
            ["set 1 Voices 1", "note-off 1/0", "note-off 1/1", "note-on 1/0 0.333 retrigger"]);
        await Assert.That(_audio.Calls[^1]).IsEqualTo("note-on 1/0 0.333 retrigger").Because("the held key sounds once its voice is built again");
        await Assert.That(_rack.Notes.Held).IsEquivalentTo([0, 4]);
        _rack.ReleaseAll();
        _audio.Calls.Clear();
        await Assert.That(_rack.Connect(new JackRef(Keyboard, Modules.Pitch), new JackRef(Lfo, Modules.Lfo.FindParameter(Modules.Rate)!.CvJack))).Succeeded();

        string[] voiced = [.. _rack.Patch.Modules.Where(m => m.Kind is ModuleKind.Keyboard or ModuleKind.Oscillators or ModuleKind.Filter
            or ModuleKind.Vca or ModuleKind.Envelope).Select(m => $"voices {m.Id} 1")];
        await Assert.That(built).IsEquivalentTo(voiced);
        await Assert.That(_audio.Calls).Contains($"voices {Lfo} 1").Because("a module the keys come to reach is built for the voices they play");
    }

    [Test]
    public async Task The_line_input_is_asked_for_only_once_a_cable_is_patched_from_it()
    {
        var lineIn = Preset.Module(ModuleKind.LineIn);
        var cable = new Cable(new JackRef(lineIn, Modules.Audio), new JackRef(Preset.Drive, Modules.In));
        await Assert.That(_audio.Calls.Where(c => c.Contains("line-in"))).IsEmpty();
        _audio.Calls.Clear();

        await Assert.That(_rack.Connect(cable.To, cable.From)).Succeeded();

        await Assert.That(_audio.Log).IsEqualTo(Lines($"disconnect {new Cable(new JackRef(Preset.Vca, Modules.Out), cable.To)}", $"connect {cable}", $"use-line-in {lineIn}"));
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    /// <summary>
    /// An audio that records what it is sent. What it reads back keeps WebSynthAudio's timing: a test sets what the audio
    /// does now (<see cref="Running"/>, <see cref="Speakers"/>, <see cref="Peaks"/>, <see cref="Knobs"/>), and the readbacks
    /// answer from the last <see cref="Refresh"/>. Before the first refresh it answers whether it runs as it is asked, and
    /// reads no levels; a knob is read as it is asked the first time, then from the refreshes after; newly metered outputs
    /// read no peaks until the next refresh; and a stopped audio reads no levels.
    /// </summary>
    internal sealed class RecordingAudio : ISynthAudio
    {
        public List<string> Calls { get; } = [];

        public string Log => string.Join("\n", Calls);

        public bool Running { get; set; } = true;

        public float[] Speakers { get; set; } = [0, 0];

        public Dictionary<(ModuleId, string), float> Knobs { get; } = [];

        public Dictionary<JackRef, float> Peaks { get; } = [];

        public List<JackRef> Metered { get; private set; } = [];

        private sealed record Frame(bool Running, float[] Speakers, float[] Peaks, Dictionary<(ModuleId, string), float> Knobs);

        private Frame? _frame;
        private readonly HashSet<(ModuleId, string)> _watched = [];

        public void Refresh() => _frame = new Frame(
            Running,
            [.. Speakers],
            Running ? [.. Metered.Select(o => Peaks.GetValueOrDefault(o))] : [],
            _watched.ToDictionary(k => k, k => Knobs.GetValueOrDefault(k)));

        public bool IsRunning => _frame?.Running ?? Running;

        public float OutputLevel => 0;

        public int ReadSpeakers(Span<float> peaks)
        {
            if (_frame is not { Running: true } frame)
            {
                return 0;
            }

            frame.Speakers.CopyTo(peaks);
            return frame.Speakers.Length;
        }

        public int? SequencerStep(ModuleId sequencer) => null;

        public LineInStatus LineInStatus { get; set; } = new(LineInState.Idle);

        public float ReadKnob(ModuleId module, string parameter)
        {
            if (_frame is { } frame && frame.Knobs.TryGetValue((module, parameter), out var read))
            {
                return read;
            }

            _watched.Add((module, parameter));
            return Knobs.GetValueOrDefault((module, parameter));
        }

        public float SampleRate => 48000;

        public int ReadScope(ModuleId scope, Span<float> samples) => 0;

        public void MeterOutputs(IReadOnlyList<JackRef> outputs)
        {
            Metered = [.. outputs];
            if (_frame is { } frame)
            {
                _frame = frame with { Peaks = [] };
            }
        }

        public int ReadLevels(Span<float> peaks)
        {
            var read = _frame?.Peaks ?? [];
            read.CopyTo(peaks);
            return read.Length;
        }

        public void AddModule(ModuleId module, ModuleKind kind, int voices) => Calls.Add($"add {module} {kind} x{voices}");

        // Building a module's voices again tears down its outputs: what they gave the meters is gone until the new voices sound
        public void SetVoices(ModuleId module, int voices)
        {
            Calls.Add($"voices {module} {voices}");
            foreach (var output in Peaks.Keys.Where(o => o.Module == module).ToList())
            {
                Peaks.Remove(output);
            }
        }

        public void SetParameter(ModuleId module, string parameter, float value) => Calls.Add($"set {module} {parameter} {value:0.###}");

        public void Connect(Cable cable) => Calls.Add($"connect {cable}");

        public void Disconnect(Cable cable) => Calls.Add($"disconnect {cable}");

        public void NoteOn(ModuleId keyboard, int voice, float pitch, bool retrigger) =>
            Calls.Add($"note-on {keyboard}/{voice} {pitch:0.###} {(retrigger ? "retrigger" : "legato")}");

        public void NoteOff(ModuleId keyboard, int voice) => Calls.Add($"note-off {keyboard}/{voice}");

        public LineInStatus LineIn(ModuleId lineIn) => LineInStatus;

        public void UseLineIn(ModuleId lineIn) => Calls.Add($"use-line-in {lineIn}");

        public void StepLineIn(ModuleId lineIn, int step) => Calls.Add($"step-line-in {lineIn} {step}");
    }
}
