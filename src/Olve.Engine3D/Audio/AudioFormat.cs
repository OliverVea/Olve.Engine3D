namespace Olve.Engine3D.Audio;

/// <summary>
/// The format of the samples an audio output plays.
/// </summary>
/// <param name="SampleRate">Frames per second.</param>
/// <param name="Channels">Samples per frame, one per channel.</param>
public readonly record struct AudioFormat(int SampleRate, int Channels);
