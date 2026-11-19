using Olve.Engine3D.Audio.Synthesis;

namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record DistortionDocument(string Name, DistortionCurve Curve, float Drive) : NodeDocument(Name);
