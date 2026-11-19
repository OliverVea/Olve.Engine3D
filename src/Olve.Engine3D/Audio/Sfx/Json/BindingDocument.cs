namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed record BindingDocument(Guid Slot, Guid? Sound, float? PitchSemitones, float? GainDb);
