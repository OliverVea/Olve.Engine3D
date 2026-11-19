using System.Text.Json.Serialization;

namespace Olve.Engine3D.Audio.Sfx.Json;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(OscillatorDocument), "oscillator")]
[JsonDerivedType(typeof(ConstantDocument), "constant")]
[JsonDerivedType(typeof(NoiseDocument), "noise")]
[JsonDerivedType(typeof(FilterDocument), "filter")]
[JsonDerivedType(typeof(DistortionDocument), "distortion")]
[JsonDerivedType(typeof(GainDocument), "gain")]
[JsonDerivedType(typeof(EnvelopeDocument), "envelope")]
public abstract record NodeDocument(string Name);
