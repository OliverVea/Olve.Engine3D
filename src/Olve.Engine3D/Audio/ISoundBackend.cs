namespace Olve.Engine3D.Audio;

public interface ISoundBackend
{
    Result CreateSound(SoundId id, ReadOnlySpan<float> samples, int sampleRate);

    DeletionResult DeleteSound(SoundId id);
}

public sealed class SilentSoundBackend : ISoundBackend
{
    public Result CreateSound(SoundId id, ReadOnlySpan<float> samples, int sampleRate) => Result.Success();

    public DeletionResult DeleteSound(SoundId id) => DeletionResult.Success();
}
