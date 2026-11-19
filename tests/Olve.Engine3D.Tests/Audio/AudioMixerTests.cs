using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Audio;

public class AudioMixerTests
{
    private const int SampleRate = 48_000;
    private const int BlockFrames = 512;

    private sealed class Rig
    {
        public Rig(bool open = true)
        {
            var audio = new SoftwareAudioBackend(Backend);
            Devices = new AudioDeviceManager(audio, NullLogger<AudioDeviceManager>.Instance);
            Sounds = SoundRendering.CreateSoundManager(audio);
            Mixer = new AudioMixer(Devices, Sounds, NullLogger<AudioMixer>.Instance);
            if (open)
            {
                Devices.Open(new AudioDeviceSelection.Default(), new AudioOutputSettings());
            }

            Bus = Mixer.AddBus(new BusName("test"));
        }

        public FakeAudioStreamBackend Backend { get; } = new();
        public AudioDeviceManager Devices { get; }
        public SoundManager Sounds { get; }
        public AudioMixer Mixer { get; }
        public BusId Bus { get; }
        public Id<AudioSegment> Segment { get; } = Id.New<AudioSegment>();

        public SoundId Register(Patch patch, double seconds)
        {
            var sound = Queue(patch, seconds);
            Sounds.RenderAll();
            return sound;
        }

        public SoundId Queue(Patch patch, double seconds) =>
            Sounds.Register(patch, TimeSpan.FromSeconds(seconds), Segment).TryPickProblems(out var problems, out var sound)
                ? throw new InvalidOperationException(string.Join("; ", problems.Select(p => p.ToDebugString())))
                : sound;

        public VoiceId Play(SoundId sound, VoiceSettings? settings = null) =>
            Mixer.Play(sound, Bus, settings ?? new VoiceSettings(), Segment).TryPickProblems(out var problems, out var voice)
                ? throw new InvalidOperationException(string.Join("; ", problems.Select(p => p.ToDebugString())))
                : voice;

        /// <summary>Renders blocks as the output's thread would, then lets the main thread hear which voices ended.</summary>
        public (float[] Left, float[] Right) Render(int blocks)
        {
            var channels = Backend.Opened[0].Render(blocks);
            Mixer.Update();
            return (channels[0], channels[1]);
        }
    }

    // A level held for the gate: an envelope that goes straight to its sustain
    private static Patch Level(float level) => new(
        [new PatchNode.Envelope("level", TimeSpan.Zero, TimeSpan.Zero, level, TimeSpan.Zero)], [], [], "level");

    private static Patch Tone(float frequency) => new([new PatchNode.Oscillator("tone", Waveform.Sine, frequency)], [], [], "tone");

    private static double Rms(ReadOnlySpan<float> samples)
    {
        var sum = 0.0;
        foreach (var sample in samples)
        {
            sum += sample * sample;
        }

        return double.Sqrt(sum / samples.Length);
    }

    [Test]
    public async Task A_Voice_Is_Panned_With_Equal_Power()
    {
        var rig = new Rig();
        var sound = rig.Register(Level(0.5f), 1);

        rig.Play(sound);
        var (left, right) = rig.Render(1);

        await Assert.That(left[100]).IsEqualTo(0.5f * MathF.Sqrt(0.5f)).Within(1e-4f);
        await Assert.That(right[100]).IsEqualTo(0.5f * MathF.Sqrt(0.5f)).Within(1e-4f);

        var panned = new Rig();
        panned.Play(panned.Register(Level(0.5f), 1), new VoiceSettings(Pan: -1));
        (left, right) = panned.Render(1);

        await Assert.That(left[100]).IsEqualTo(0.5f).Within(1e-4f);
        await Assert.That(right[100]).IsEqualTo(0f).Within(1e-4f);
    }

    [Test]
    public async Task A_Voice_Ends_With_Its_Sound_And_Sooner_At_A_Higher_Pitch()
    {
        var rig = new Rig();
        var sound = rig.Register(Level(0.5f), 0.1);

        rig.Play(sound, new VoiceSettings(Pan: -1));
        rig.Play(sound, new VoiceSettings(Pitch: 2, Pan: 1));
        var (left, right) = rig.Render(20);

        await Assert.That(left.Count(s => s != 0)).IsBetween(4790, 4800);
        await Assert.That(right.Count(s => s != 0)).IsBetween(2390, 2400);
        await Assert.That(rig.Mixer.Voices).IsEmpty();
    }

    [Test]
    public async Task A_Looping_Voice_Plays_Until_Stopped()
    {
        var rig = new Rig();
        var voice = rig.Play(rig.Register(Level(0.5f), 0.01), new VoiceSettings(Loop: true));

        var (left, _) = rig.Render(20);

        await Assert.That(left[^1]).IsGreaterThan(0.3f);
        await Assert.That(rig.Mixer.Voices.Count()).IsEqualTo(1);

        rig.Mixer.Stop(voice, TimeSpan.FromMilliseconds(5));
        (left, _) = rig.Render(2);

        await Assert.That(left[^1]).IsEqualTo(0f);
        await Assert.That(rig.Mixer.Voices).IsEmpty();
    }

    [Test]
    public async Task Releasing_A_Looping_Voice_Lets_It_Play_To_The_End_Of_Its_Sound()
    {
        var rig = new Rig();
        var voice = rig.Play(rig.Register(Level(0.5f), 0.1), new VoiceSettings(Loop: true));
        rig.Render(20);

        rig.Mixer.Release(voice);
        rig.Render(20);

        await Assert.That(rig.Mixer.Voices).IsEmpty();
    }

    [Test]
    public async Task A_Gain_Change_Ramps_Without_A_Jump()
    {
        var rig = new Rig();
        var voice = rig.Play(rig.Register(Level(0.5f), 1));
        rig.Render(1);

        rig.Mixer.Set(voice, VoiceParameter.Gain, 0, TimeSpan.FromMilliseconds(10));
        var (left, _) = rig.Render(1);

        await Assert.That(left[0]).IsGreaterThan(0.3f);
        await Assert.That(left[240]).IsBetween(0.15f, 0.2f);
        await Assert.That(left[^1]).IsEqualTo(0f);
        await Assert.That(rig.Mixer.Voices.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task A_Bus_Sets_The_Gain_And_Cutoff_Of_What_It_Mixes()
    {
        var rig = new Rig();
        rig.Play(rig.Register(Tone(8000), 1));
        rig.Mixer.Set(rig.Bus, BusParameter.Cutoff, 500, TimeSpan.Zero);

        var (filtered, _) = rig.Render(20);

        await Assert.That(Rms(filtered.AsSpan(BlockFrames * 10))).IsLessThan(0.01);

        rig.Mixer.Set(rig.Bus, BusParameter.Cutoff, 20_000, TimeSpan.Zero);
        rig.Mixer.Set(rig.Bus, BusParameter.Gain, 0.5f, TimeSpan.Zero);
        var (open, _) = rig.Render(20);

        await Assert.That(Rms(open.AsSpan(BlockFrames * 10))).IsEqualTo(0.25).Within(0.01);
    }

    [Test]
    public async Task The_Limiter_Keeps_The_Mix_From_Clipping()
    {
        var rig = new Rig();
        var sound = rig.Register(Level(0.5f), 1);
        for (var i = 0; i < 4; i++)
        {
            rig.Play(sound, new VoiceSettings(Pan: -1));
        }

        var (left, _) = rig.Render(10);

        await Assert.That(left.Max()).IsLessThanOrEqualTo(AudioEngine.LimiterThreshold + 1e-4f);
        await Assert.That(left[^1]).IsEqualTo(AudioEngine.LimiterThreshold).Within(1e-3f);
    }

    [Test]
    public async Task Releasing_A_Segment_Fades_Its_Voices_And_Deletes_Its_Sounds()
    {
        var rig = new Rig();
        var sound = rig.Register(Level(0.5f), 1);
        rig.Play(sound, new VoiceSettings(Loop: true));
        rig.Render(1);

        rig.Mixer.StopSegment(rig.Segment);
        rig.Sounds.DeleteSegment(rig.Segment);
        rig.Render(4);

        await Assert.That(rig.Mixer.Voices).IsEmpty();
        await Assert.That(rig.Sounds.Count).IsEqualTo(0);
        await Assert.That(rig.Mixer.Play(sound, rig.Bus, new VoiceSettings(), rig.Segment)).Failed();
    }

    [Test]
    public async Task A_Voice_Plays_On_After_Its_Sound_Is_Deleted()
    {
        var rig = new Rig();
        var sound = rig.Register(Level(0.5f), 1);
        rig.Play(sound);

        await Assert.That(rig.Sounds.Delete(sound).WasNotFound).IsFalse();
        var (left, _) = rig.Render(4);

        await Assert.That(left[^1]).IsGreaterThan(0.3f);
        await Assert.That(rig.Mixer.Voices.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task While_The_Output_Is_Closed_Voices_End_At_Once()
    {
        var rig = new Rig(open: false);

        await Assert.That(rig.Mixer.Play(rig.Register(Level(0.5f), 1), rig.Bus, new VoiceSettings(), rig.Segment)).Succeeded();
        await Assert.That(rig.Mixer.Voices).IsEmpty();
    }

    [Test]
    public async Task A_Sound_Renders_Over_Frames_And_Plays_Only_Once_Rendered()
    {
        var rig = new Rig();
        var deleted = rig.Queue(Level(0.5f), 1);
        var sound = rig.Queue(Level(0.5f), 5.0 * PatchRenderer.BlockFrames / SampleRate);

        rig.Sounds.Update();
        await Assert.That(rig.Sounds.Delete(deleted).WasNotFound).IsFalse();
        rig.Sounds.Update();
        rig.Sounds.Update();

        await Assert.That(Problem(rig.Mixer.Play(sound, rig.Bus, new VoiceSettings(), rig.Segment)))
            .IsEqualTo($"Sound {sound} is still rendering");
        await Assert.That(Problem(rig.Mixer.Play(deleted, rig.Bus, new VoiceSettings(), rig.Segment)))
            .IsEqualTo($"No sound {deleted}");

        rig.Sounds.Update();
        await Assert.That((rig.Sounds.Count, rig.Sounds.RenderingCount)).IsEqualTo((1, 0));
        await Assert.That(rig.Mixer.Play(sound, rig.Bus, new VoiceSettings(), rig.Segment)).Succeeded();
    }

    private static string Problem<T>(Result<T> result) =>
        result.TryPickProblems(out var problems) ? string.Join(": ", problems.Select(problem => problem.ToBriefString())) : "succeeded";

    [Test]
    public async Task Playing_On_An_Unknown_Bus_Fails()
    {
        var rig = new Rig();
        var sound = rig.Register(Level(0.5f), 1);

        await Assert.That(rig.Mixer.Play(sound, BusId.New(), new VoiceSettings(), rig.Segment)).Failed();
        await Assert.That(rig.Mixer.Play(SoundId.New(), rig.Bus, new VoiceSettings(), rig.Segment)).Failed();
    }
}
