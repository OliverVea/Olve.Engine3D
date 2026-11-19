using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Input;
using Olve.Engine3D.Math;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Tests.Samples.Synth;

/// <summary>Patching by clicks: a click on a jack picks a cable up, and the next click plugs, drops or puts it back.</summary>
public class RackClickPatchTests
{
    private static readonly JackRef LfoOut = new(Preset.Lfo, Modules.Out);
    private static readonly JackRef EnvelopeOut = new(Preset.Envelope, Modules.Out);
    private static readonly JackRef FilterCutoff = Preset.CvOf(Preset.Filter, Modules.Cutoff);
    private static readonly JackRef VcaLevelCv = new(Preset.Vca, Modules.Vca.FindParameter(Modules.Level)!.CvJack);

    private readonly PlatformEvents _events = new();
    private readonly InputState _input = new(new TestWindowInfo { ContentAreaSize = new Vector2<int>(1280, 800) }, [Pointer.Mouse]);
    private readonly SynthRackTests.RecordingAudio _audio = new();
    private readonly SynthRack _rack;
    private readonly RackInputService _service;

    public RackClickPatchTests()
    {
        _rack = new SynthRack(_audio, NullLogger<SynthRack>.Instance);
        _service = new RackInputService(_rack, _input, new TestWindowInfo { ContentAreaSize = new Vector2<int>(1280, 800) }, new DeltaTimeService(), NullLogger<RackInputService>.Instance);
        // The preset, with the LFO patched into the filter's cutoff
        if (_service.Load().TryPickProblems(out _) || _rack.Connect(LfoOut, FilterCutoff).TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack didn't load");
        }

        Frame();
    }

    private void Frame()
    {
        // As AudioFrameService does, ahead of the rack's input
        _audio.Refresh();
        _input.Apply(_events.Take());
        if (_service.Input().TryPickProblems(out _))
        {
            throw new InvalidOperationException("The rack's input failed");
        }
    }

    private Vector2 At(JackRef jack) => _service.View.ToScreen(_service.Layout.FindJack(jack)!.Centre);

    // Off the rack: nothing is there
    private static readonly Vector2 Empty = new(2, 2);

    private void Click(Vector2 at, Button? button = null)
    {
        _events.AddAxis(Pointer.Mouse.Position, at);
        _events.AddButtonDown(button ?? Pointer.Mouse.Primary);
        Frame();
        _events.AddButtonUp(button ?? Pointer.Mouse.Primary);
        Frame();
    }

    private void Tap(int finger, Vector2 at)
    {
        var touch = Pointer.Touch(finger);
        _events.AddPointer(touch);
        _events.AddAxis(touch.Position, at);
        _events.AddButtonDown(touch.Primary);
        Frame();
        _events.AddButtonUp(touch.Primary);
        _events.RemovePointer(touch);
        Frame();
    }

    private void Click(JackRef jack) => Click(At(jack));

    private JackRef? Carried => (_service.Carried?.Drag as RackDrag.Cable)?.Fixed;

    [Test]
    public async Task Clicking_a_jack_picks_up_a_cable_whose_end_follows_the_mouse_and_clicking_a_free_input_plugs_it()
    {
        var cables = _rack.Patch.Cables.Count;

        Click(EnvelopeOut);
        await Assert.That(Carried).IsEqualTo(EnvelopeOut);
        await Assert.That(_rack.Patch.Cables.Count).IsEqualTo(cables);
        await Assert.That(_service.Hint).IsEqualTo(new RackHint("CLICK A GLOWING JACK TO PLUG, ELSEWHERE TO UNPLUG, ESC TO PUT BACK", HintTone.Help));
        await Assert.That(_service.Drags.Select(d => d.Drag)).IsEquivalentTo([(RackDrag)new RackDrag.Cable(EnvelopeOut)]);

        _events.AddAxis(Pointer.Mouse.Position, At(VcaLevelCv));
        Frame();
        await Assert.That(Vector2.Distance(_service.Carried!.Position, _service.Layout.FindJack(VcaLevelCv)!.Centre)).IsLessThan(0.01f);
        await Assert.That(_service.Carried.Hovered).IsEqualTo(new RackHit.Jack(_service.Layout.FindJack(VcaLevelCv)!));

        Click(VcaLevelCv);
        await Assert.That(_rack.Patch.CableInto(VcaLevelCv)).IsEqualTo(new Cable(EnvelopeOut, VcaLevelCv));
        await Assert.That(_service.Carried).IsNull();
        await Assert.That(_service.Hint).IsEqualTo(new RackHint("PLAY A-', Z/X FOR OCTAVES. CLICK OR DRAG JACK TO JACK TO PATCH", HintTone.Help));
    }

    [Test]
    public async Task Clicking_a_plugged_input_picks_that_end_up_again_to_plug_it_elsewhere()
    {
        Click(FilterCutoff);
        await Assert.That(Carried).IsEqualTo(LfoOut);
        await Assert.That(_rack.Patch.CableInto(FilterCutoff)).IsNull();

        Click(VcaLevelCv);
        await Assert.That(_rack.Patch.CableInto(VcaLevelCv)).IsEqualTo(new Cable(LfoOut, VcaLevelCv));
        await Assert.That(_rack.Patch.CableInto(FilterCutoff)).IsNull();
    }

    [Test]
    public async Task Clicking_empty_space_with_a_cable_in_hand_leaves_it_unplugged()
    {
        Click(FilterCutoff);
        Click(Empty);

        await Assert.That(_service.Carried).IsNull();
        await Assert.That(_rack.Patch.CablesAt(LfoOut).Select(c => c.To)).DoesNotContain(FilterCutoff);
        await Assert.That(_rack.Patch.CableInto(FilterCutoff)).IsNull();
    }

    public static IEnumerable<Func<(string Name, Action<RackClickPatchTests> Cancel)>> Cancels() =>
    [
        () => ("Escape", test =>
        {
            test._events.AddButtonDown(Key.Escape);
            test.Frame();
            test._events.AddButtonUp(Key.Escape);
            test.Frame();
        }),
        () => ("right-click", test => test.Click(Empty, Pointer.Mouse.Secondary)),
        () => ("a click on the jack it hangs from", test => test.Click(LfoOut)),
    ];

    [Test]
    [MethodDataSource(nameof(Cancels))]
    public async Task Cancelling_puts_a_picked_up_cable_back_where_it_was(string name, Action<RackClickPatchTests> cancel)
    {
        Click(FilterCutoff);
        cancel(this);

        await Assert.That(_service.Carried).IsNull().Because(name);
        await Assert.That(_rack.Patch.CableInto(FilterCutoff)).IsEqualTo(new Cable(LfoOut, FilterCutoff)).Because(name);
    }

    [Test]
    public async Task A_jack_the_cable_cant_plug_into_says_why_and_the_cable_stays_in_hand()
    {
        Click(EnvelopeOut);
        Click(LfoOut);

        await Assert.That(_service.Hint).IsEqualTo(new RackHint("A CABLE JOINS AN OUTPUT TO AN INPUT, NOT TWO OUTPUTS", HintTone.Problem));
        await Assert.That(Carried).IsEqualTo(EnvelopeOut);

        Click(VcaLevelCv);
        await Assert.That(_rack.Patch.CableInto(VcaLevelCv)).IsEqualTo(new Cable(EnvelopeOut, VcaLevelCv));
        await Assert.That(_service.Hint.Tone).IsEqualTo(HintTone.Help);
    }

    [Test]
    public async Task Until_the_audio_runs_the_hint_says_to_start_it()
    {
        _audio.Running = false;
        Frame();

        await Assert.That(_service.Hint).IsEqualTo(new RackHint("TAP OR CLICK TO START AUDIO, THEN PLAY THE KEYS", HintTone.Start));
    }

    [Test]
    public async Task A_tap_works_like_a_click()
    {
        Tap(1, At(EnvelopeOut));
        await Assert.That(Carried).IsEqualTo(EnvelopeOut);

        Tap(2, At(VcaLevelCv));
        await Assert.That(_rack.Patch.CableInto(VcaLevelCv)).IsEqualTo(new Cable(EnvelopeOut, VcaLevelCv));
        await Assert.That(_service.Carried).IsNull();
        await Assert.That(_service.Drags).IsEmpty();
    }

    [Test]
    public async Task Dragging_jack_to_jack_still_patches_without_picking_anything_up()
    {
        _events.AddAxis(Pointer.Mouse.Position, At(EnvelopeOut));
        _events.AddButtonDown(Pointer.Mouse.Primary);
        Frame();
        _events.AddAxis(Pointer.Mouse.Position, At(VcaLevelCv));
        _events.AddButtonUp(Pointer.Mouse.Primary);
        Frame();

        await Assert.That(_rack.Patch.CableInto(VcaLevelCv)).IsEqualTo(new Cable(EnvelopeOut, VcaLevelCv));
        await Assert.That(_service.Carried).IsNull();
    }
}
