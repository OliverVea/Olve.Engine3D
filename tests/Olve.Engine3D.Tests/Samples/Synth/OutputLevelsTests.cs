using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Time;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class OutputLevelsTests
{
    // A peak's magnitude and the lights it lights: 12 at full scale, none 42 dB down or quieter
    [Test]
    [Arguments(1f, 12f)]
    [Arguments(0.5f, 10.28f)]
    [Arguments(0.1f, 6.29f)]
    [Arguments(0.007f, 0f)]
    [Arguments(0f, 0f)]
    [Arguments(2f, 12f)]
    public async Task A_peak_lights_the_ring_by_its_level_in_decibels(float peak, float lit)
    {
        await Assert.That(OutputMeter.Lit(OutputMeter.Step(OutputMeter.Floor, peak, 0))).IsEqualTo(lit).Within(0.01f);
    }

    [Test]
    public async Task Every_output_is_metered_and_its_ring_jumps_to_a_peak_then_falls_back_over_a_second()
    {
        var audio = new SynthRackTests.RecordingAudio();
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        await Assert.That(rack.Load(Presets.Classic())).Succeeded();
        var time = new DeltaTimeService();
        var levels = new OutputLevelsService(rack, time);
        var vca = new JackRef(Preset.Vca, Modules.Out);
        time.SetFrameDelta(TimeSpan.FromSeconds(1 / 60.0));

        audio.Peaks[vca] = 1;
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(audio.Metered).IsEquivalentTo(rack.Patch.Modules.SelectMany(m =>
            m.Definition.Jacks.Where(j => j.Direction == JackDirection.Output).Select(j => new JackRef(m.Id, j.Name))));
        await Assert.That(levels.Lit(vca)).IsEqualTo(0).Because("the peaks of outputs metered from now on are read back from the next frame");
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(levels.Lit(vca)).IsEqualTo(OutputMeter.Lights);
        await Assert.That(levels.Lit(new JackRef(Preset.Lfo, Modules.Out))).IsEqualTo(0);

        audio.Peaks[vca] = 0;
        time.SetFrameDelta(TimeSpan.FromSeconds(0.5));
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(levels.Lit(vca)).IsEqualTo(OutputMeter.Lights * (1 - 15 / 42f)).Within(0.01f);
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(levels.Lit(vca)).IsEqualTo(0);
    }

    [Test]
    public async Task Each_speaker_is_metered_on_its_own_so_a_signal_panned_left_lights_only_the_left()
    {
        var audio = new SynthRackTests.RecordingAudio { Speakers = [1, 0] };
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        var time = new DeltaTimeService();
        var speakers = new SpeakerLevelsService(rack, time);
        time.SetFrameDelta(TimeSpan.FromSeconds(1 / 60.0));

        audio.Refresh();
        await Assert.That(speakers.Update()).Succeeded();

        await Assert.That(speakers.Lit(Speaker.Left)).IsEqualTo(OutputMeter.Lights);
        await Assert.That(speakers.Lit(Speaker.Right)).IsEqualTo(0);
    }

    [Test]
    public async Task Loudness_rises_quickly_with_the_output_and_falls_slowly_and_a_frames_flicker_barely_moves_it()
    {
        var loudness = 0f;
        for (var frame = 0; frame < 15; frame++)
        {
            loudness = Loudness.Step(loudness, Loudness.Full, 1 / 60f);
        }

        // A quarter of a second of a loud output: nearly full
        await Assert.That(loudness).IsGreaterThan(0.95f);

        var quiet = Loudness.Step(loudness, 0, 1 / 60f);
        await Assert.That(quiet).IsGreaterThan(0.9f).Because("one quiet frame barely lowers it");
        for (var frame = 0; frame < 60; frame++)
        {
            quiet = Loudness.Step(quiet, 0, 1 / 60f);
        }

        await Assert.That(quiet).IsLessThan(0.15f).Because("a second of silence takes it nearly to nothing");
        await Assert.That(Loudness.Step(0, 10 * Loudness.Full, 10)).IsEqualTo(1);
    }

    [Test]
    public async Task A_module_built_again_for_its_voices_keeps_its_meter_which_reads_the_new_voices()
    {
        var audio = new SynthRackTests.RecordingAudio();
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        await Assert.That(rack.Load(Presets.Classic())).Succeeded();
        var time = new DeltaTimeService();
        time.SetFrameDelta(TimeSpan.FromSeconds(5));
        var levels = new OutputLevelsService(rack, time);
        var lfo = new JackRef(Preset.Lfo, Modules.Out);
        audio.Peaks[lfo] = 1;
        await Assert.That(Frame(audio, levels)).Succeeded();
        var metered = audio.Metered;

        // The keys' pitch in the LFO's rate gives it a voice per note: its outputs are torn down and built again
        await Assert.That(rack.Connect(new JackRef(Preset.Keyboard, Modules.Pitch), Preset.CvOf(Preset.Lfo, Modules.Rate))).Succeeded();
        await Assert.That(audio.Calls).Contains($"voices {Preset.Lfo} {Modules.MaxVoices}");
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(levels.Lit(lfo)).IsEqualTo(0);

        audio.Peaks[lfo] = 1;
        await Assert.That(Frame(audio, levels)).Succeeded();
        await Assert.That(levels.Lit(lfo)).IsEqualTo(OutputMeter.Lights);
        await Assert.That(audio.Metered).IsSameReferenceAs(metered).Because("the outputs are the same, so they aren't metered afresh");
    }

    // A frame as the scene runs it: the audio read back (AudioFrameService), then the meters updated
    private static Olve.Results.Result Frame(SynthRackTests.RecordingAudio audio, OutputLevelsService levels)
    {
        audio.Refresh();
        return levels.Update();
    }
}
