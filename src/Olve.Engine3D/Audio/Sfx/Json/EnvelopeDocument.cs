namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record EnvelopeDocument(string Name, TimeSpan Attack, TimeSpan Decay, float Sustain, TimeSpan Release) : NodeDocument(Name);
