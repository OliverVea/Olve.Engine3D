namespace Olve.Engine3D.Audio;

/// <summary>What an audio device plays: it asks for samples on its own audio thread.</summary>
public interface IAudioStream
{
    /// <summary>Fills the buffer, overwriting it. Called on the output's audio thread only.</summary>
    void Render(SampleBuffer buffer, int sampleRate);
}
