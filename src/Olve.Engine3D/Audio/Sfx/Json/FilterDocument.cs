using Olve.Engine3D.Audio.Synthesis;

namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record FilterDocument(string Name, FilterType FilterType, float Cutoff, float Q, float GainDb) : NodeDocument(Name);
