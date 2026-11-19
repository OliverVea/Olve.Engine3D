using Olve.Engine3D.Audio.Synthesis;

namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record OscillatorDocument(string Name, Waveform Waveform, float Frequency) : NodeDocument(Name);
