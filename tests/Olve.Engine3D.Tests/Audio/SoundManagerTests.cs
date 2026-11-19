using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Results;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Audio;

public class SoundManagerTests
{
    private sealed class RecordingSoundBackend : ISoundBackend
    {
        public List<string> Calls { get; } = [];

        public Result CreateSound(SoundId id, ReadOnlySpan<float> samples, int sampleRate)
        {
            Calls.Add($"create {id} {samples.Length} {sampleRate}");
            return Result.Success();
        }

        public DeletionResult DeleteSound(SoundId id)
        {
            Calls.Add($"delete {id}");
            return DeletionResult.Success();
        }
    }

    private static readonly Patch Level = new(
        [new PatchNode.Envelope("level", TimeSpan.Zero, TimeSpan.Zero, 0.5f, TimeSpan.Zero)], [], [], "level");

    private const int Frames = PatchRenderer.BlockFrames;

    public enum Deletion
    {
        Sound,
        Segment,
    }

    [Test]
    [Arguments(Deletion.Sound)]
    [Arguments(Deletion.Segment)]
    public async Task The_Backend_Holds_A_Sound_From_When_It_Renders_Until_It_Is_Deleted(Deletion deletion)
    {
        var backend = new RecordingSoundBackend();
        var sounds = SoundRendering.CreateSoundManager(backend);
        var segment = Id.New<AudioSegment>();
        var sound = Register(sounds, segment, Frames);
        var rendering = Register(sounds, segment, 8 * Frames);

        sounds.Update();
        await Assert.That(backend.Calls).IsEquivalentTo([$"create {sound} {Frames} {SoundManager.SampleRate}"]);

        Delete(sounds, deletion, segment, sound, rendering);

        await Assert.That(backend.Calls).IsEquivalentTo([$"create {sound} {Frames} {SoundManager.SampleRate}", $"delete {sound}"]);
        sounds.RenderAll();
        await Assert.That(backend.Calls.Count).IsEqualTo(2);
    }

    public enum Budget
    {
        RealTime,
        Whole,
    }

    [Test]
    [Arguments(Budget.RealTime, false)]
    [Arguments(Budget.Whole, true)]
    public async Task A_Sound_Renders_Within_The_Frame_Budget(Budget budget, bool renderedInOneFrame)
    {
        var backend = new RecordingSoundBackend();
        var sounds = SoundRendering.CreateSoundManager(backend,
            budget == Budget.Whole ? SoundRenderBudget.Whole : SoundRenderBudget.RealTime);
        var sound = Register(sounds, Id.New<AudioSegment>(), 8 * Frames);

        sounds.Update();

        await Assert.That(sounds.IsRendering(sound)).IsEqualTo(!renderedInOneFrame);
    }

    private static SoundId Register(SoundManager sounds, Id<AudioSegment> segment, int frames) =>
        sounds.Register(Level, TimeSpan.FromSeconds((double)frames / SoundManager.SampleRate), segment)
            .TryPickProblems(out _, out var sound)
            ? throw new InvalidOperationException()
            : sound;

    private static void Delete(SoundManager sounds, Deletion deletion, Id<AudioSegment> segment, params SoundId[] ids)
    {
        if (deletion == Deletion.Segment)
        {
            sounds.DeleteSegment(segment);
            return;
        }

        foreach (var id in ids)
        {
            _ = sounds.Delete(id);
        }
    }
}
