using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class KeyVoicesTests
{
    // Keys pressed (+) and released (-) on so many voices, and what each did: "voice:semitone!" plays and opens the gate
    // again, "voice:semitone~" glides to the key without opening it, "voice:off" closes it and "-" changes nothing
    [Test]
    // One voice: the newest key sounds, and releasing it returns to the newest still held
    [Arguments(1, "+0 +4", "0:0! 0:4!")]
    [Arguments(1, "+0 +4 +7 -7 -4", "0:0! 0:4! 0:7! 0:4~ 0:0~")]
    [Arguments(1, "+0 +4 -0 -4", "0:0! 0:4! - 0:off")]
    [Arguments(1, "+0 +4 +0 -0", "0:0! 0:4! 0:0! 0:4~")]
    [Arguments(1, "-5 +5 -5 -5", "- 0:5! 0:off -")]
    // More: each key takes the voice released longest ago, and with none free the oldest note gives its up
    [Arguments(2, "+0 +4 +7", "0:0! 1:4! 0:7!")]
    [Arguments(3, "+0 +4 -4 -0", "0:0! 1:4! 1:off 0:off")]
    [Arguments(3, "+0 +4 +7 -0 -4 +9", "0:0! 1:4! 2:7! 0:off 1:off 0:9!")]
    [Arguments(3, "+0 +4 +7 +9 -0 -4", "0:0! 1:4! 2:7! 0:9! - 1:off")]
    [Arguments(3, "+0 +4 +0", "0:0! 1:4! 0:0!")]
    [Arguments(3, "-5 +5 -5 -5", "- 0:5! 0:off -")]
    public async Task One_voice_plays_the_newest_key_and_more_play_a_key_each(int voices, string keys, string changes)
    {
        var notes = new KeyVoices();
        notes.SetVoices(voices);

        var done = keys.Split(' ').Select(step => int.Parse(step[1..]) is var semitone && step[0] == '+'
            ? notes.Press(semitone)
            : notes.Release(semitone));

        await Assert.That(string.Join(" ", done.Select(change => change switch
        {
            VoiceChange.Play play => $"{play.Voice}:{play.Semitone}{(play.Retrigger ? "!" : "~")}",
            VoiceChange.Stop stop => $"{stop.Voice}:off",
            _ => "-",
        }))).IsEqualTo(changes);
    }

    [Test]
    public async Task A_patch_loads_with_as_many_voices_as_its_keyboard_says()
    {
        var patch = Presets.Classic();
        await Assert.That(patch.SetValue(Preset.Keyboard, Modules.Voices, 1)).Succeeded();
        var rack = new SynthRack(new SynthRackTests.RecordingAudio(), NullLogger<SynthRack>.Instance);

        await Assert.That(rack.Load(patch)).Succeeded();

        await Assert.That(rack.Notes.Voices).IsEqualTo(1);
    }

    [Test]
    public async Task The_keys_cables_carry_voices_only_while_there_are_several()
    {
        var audio = new SynthRackTests.RecordingAudio();
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        await Assert.That(rack.Load(Presets.Classic())).Succeeded();
        // The keys reach the voice path through its cables
        var pitch = rack.Patch.Cables.First(c => c.From == new JackRef(Preset.Keyboard, Modules.Pitch));
        var vcaToDrive = rack.Patch.Cables.Single(c => c.From.Module == Preset.Vca && c.To.Module == Preset.Drive);
        var driveOut = rack.Patch.Cables.Single(c => c.From.Module == Preset.Drive);
        await Assert.That(rack.IsPolyphonic(pitch)).IsTrue();
        await Assert.That(rack.IsPolyphonic(vcaToDrive)).IsTrue().Because("the voices are summed where they meet the drive");
        await Assert.That(rack.IsPolyphonic(driveOut)).IsFalse();
        await Assert.That(rack.IsPolyphonic(pitch.To)).IsFalse().Because("a cable dragged from an input carries what reaches it, not yet known");

        await Assert.That(rack.SetValue(Preset.Keyboard, Modules.Voices, 1)).Succeeded();

        await Assert.That(rack.IsPolyphonic(pitch)).IsFalse();
    }

    [Test]
    public async Task The_mod_wheels_output_is_one_signal_so_patching_it_gives_no_module_voices()
    {
        var audio = new SynthRackTests.RecordingAudio();
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        await Assert.That(rack.Load(Presets.Classic())).Succeeded();
        var mod = new JackRef(Preset.Keyboard, Modules.Mod);
        var rate = new JackRef(Preset.Lfo, Modules.Lfo.FindParameter(Modules.Rate)!.CvJack);
        audio.Calls.Clear();

        await Assert.That(rack.Connect(mod, rate)).Succeeded();

        await Assert.That(audio.Log).IsEqualTo($"connect {new Cable(mod, rate)}");
        await Assert.That(rack.IsPolyphonic(new Cable(mod, rate))).IsFalse();
    }
}
