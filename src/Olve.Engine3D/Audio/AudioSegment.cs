using Microsoft.Extensions.Logging;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio;

/// <summary>
/// What a scene scope registered with the audio managers, which live as long as the app. When the scope ends, its
/// voices fade out and its sounds are deleted.
/// </summary>
public sealed class AudioSegment(AudioMixer mixer, SoundManager soundManager, ILogger<AudioSegment> logger) : IDisposable
{
    public Id<AudioSegment> Id { get; } = Olve.Utilities.Ids.Id.New<AudioSegment>();

    public static implicit operator Id<AudioSegment>(AudioSegment segment) => segment.Id;

    public void Dispose()
    {
        logger.LogDebug("Releasing audio segment {SegmentId}", Id);
        mixer.StopSegment(Id);
        soundManager.DeleteSegment(Id);
    }
}
