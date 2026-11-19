using Olve.Engine3D.Scenes;

namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>At the start of each frame, before any service reads the audio back, has it read back all at once (<see cref="ISynthAudio.Refresh"/>).</summary>
public sealed class AudioFrameService(SynthRack rack) : ISceneService
{
    // Ahead of the services at the default priority, which read the audio back
    public int Priority => -1024;

    public Result Input()
    {
        rack.Audio.Refresh();
        return Result.Success();
    }
}
