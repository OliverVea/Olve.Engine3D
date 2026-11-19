using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxBinding(Id<SfxSlot> Slot, Id<LibrarySound>? Sound, float? PitchSemitones, float? GainDb);
