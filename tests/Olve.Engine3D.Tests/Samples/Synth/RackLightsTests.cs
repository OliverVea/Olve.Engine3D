using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Samples.Synth.Space;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class RackLightsTests
{
    [Test]
    public async Task A_light_follows_its_knobs_as_the_audio_reads_them_and_a_patched_part_follows_its_signal_alone()
    {
        var audio = new SynthRackTests.RecordingAudio();
        var rack = new SynthRack(audio, NullLogger<SynthRack>.Instance);
        await Assert.That(rack.Load(Presets.Classic()).TryPickProblems(out _)).IsFalse();
        var lights = rack.Patch.Modules.Single(m => m.Kind == ModuleKind.Lights).Id;
        var lfo = rack.Patch.Modules.First(m => m.Kind == ModuleKind.Lfo).Id;
        audio.Knobs[(lights, Modules.LightKnob(LightSide.Left, Modules.Intensity))] = 0.3f;
        audio.Knobs[(lights, Modules.LightKnob(LightSide.Left, Modules.Hue))] = 0.6f;
        audio.Knobs[(lights, Modules.LightKnob(LightSide.Left, Modules.Saturation))] = 0.9f;

        var room = new RackLights();
        var unpatched = RackLights.Read(rack, LightSide.Left);
        room.Update(TimeSpan.FromSeconds(5), unpatched, RackLights.Read(rack, LightSide.Right));
        await Assert.That(unpatched).IsEqualTo(new LightReading(new LightSettings(0.3f, 0.6f, 0.9f)));
        // Unpatched, the light drifts a little round its knobs
        await Assert.That(room.Left.Intensity).IsNotEqualTo(0.3f).And.IsEqualTo(0.3f).Within(0.03f);
        await Assert.That(room.Left.Hue).IsEqualTo(0.6f).Within(0.03f);

        await Assert.That(rack.Connect(new JackRef(lfo, Modules.Out), new JackRef(lights, Modules.LightKnob(LightSide.Left, Modules.Intensity) + " " + Modules.Cv))
            .TryPickProblems(out _)).IsFalse();
        var patched = RackLights.Read(rack, LightSide.Left);
        room.Update(TimeSpan.FromSeconds(5), patched, RackLights.Read(rack, LightSide.Right));
        await Assert.That(patched.IntensityPatched).IsTrue();
        await Assert.That(room.Left.Intensity).IsEqualTo(0.3f);
    }

    [Test]
    public async Task Without_a_lights_module_the_lights_rest()
    {
        var rack = new SynthRack(new SynthRackTests.RecordingAudio(), NullLogger<SynthRack>.Instance);

        await Assert.That(RackLights.Read(rack, LightSide.Left)).IsEqualTo(new LightReading(RackLights.LeftRest));
        await Assert.That(RackLights.Read(rack, LightSide.Right)).IsEqualTo(new LightReading(RackLights.RightRest));
    }
}
