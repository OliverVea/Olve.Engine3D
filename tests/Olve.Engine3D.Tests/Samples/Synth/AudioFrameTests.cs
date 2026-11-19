using Olve.Engine3D.Samples.Synth.Playing;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class AudioFrameTests
{
    // What a frame read back says. Values: as readFrame in synth.js lays them out. Knobs: how many were watched when it was
    // read. Remeter: the outputs metered changed after it was read. Then what it reads: whether the audio runs, the
    // speakers', the metered outputs' peaks and each watched knob, and one knob more, watched since
    public static IEnumerable<Func<(double[] Values, int Watched, bool Remeter, bool Running, float[] Speakers, float[] Peaks, float?[] Knobs)>> Frames() =>
    [
        // Running: two speakers, two metered peaks, one knob
        () => ([1, 48000, 0.5, 0.2, 0.3, 2, 0.7, 0.8, 0.25], 1, false, true, [0.2f, 0.3f], [0.7f, 0.8f], [0.25f, null]),
        // Stopped: no speakers or peaks, though the room was there for them; the knob follows the peaks there are
        () => ([0, 44100, 0, 0, 0, 0, 0.4, 0, 0], 1, false, false, [], [], [0.4f, null]),
        // The outputs metered changed after the frame was read: no peaks until the next, the knobs where they were
        () => ([1, 48000, 0.5, 0.2, 0.3, 2, 0.7, 0.8, 0.25, 0.75], 2, true, true, [0.2f, 0.3f], [], [0.25f, 0.75f, null]),
        // No meters yet
        () => ([1, 48000, 0.5, 0.2, 0.3, 0, 0.6], 1, false, true, [0.2f, 0.3f], [], [0.6f, null]),
    ];

    [Test]
    [MethodDataSource(nameof(Frames))]
    public async Task A_frame_reads_back_the_audio_as_synth_js_laid_it_out(
        double[] values, int knobs, bool remeter, bool running, float[] speakers, float[] peaks, float?[] expectedKnobs)
    {
        var frame = new AudioFrame();
        var metered = (int)values[AudioFrame.MetersAt];
        values.CopyTo(frame.Into(values.Length - AudioFrame.PeaksAt - knobs, knobs));
        frame.Read(knobs);
        if (remeter)
        {
            frame.Remeter();
        }

        var speakerPeaks = new float[2];
        var levels = new float[metered];
        await Assert.That(frame.IsRunning).IsEqualTo(running);
        await Assert.That(speakerPeaks[..frame.ReadSpeakers(speakerPeaks)]).IsEquivalentTo(speakers);
        await Assert.That(levels[..frame.ReadLevels(levels)]).IsEquivalentTo(peaks);
        await Assert.That(expectedKnobs.Select((_, slot) => frame.TryReadKnob(slot, out var value) ? value : (float?)null)).IsEquivalentTo(expectedKnobs);
    }

    [Test]
    public async Task Before_a_frame_is_read_it_says_nothing()
    {
        var frame = new AudioFrame();
        var peaks = new float[2];

        await Assert.That(frame.IsRead).IsFalse();
        await Assert.That(frame.IsRunning).IsFalse();
        await Assert.That(frame.ReadSpeakers(peaks)).IsEqualTo(0);
        await Assert.That(frame.ReadLevels(peaks)).IsEqualTo(0);
        await Assert.That(frame.TryReadKnob(0, out _)).IsFalse();
    }
}
