namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxSlotState(SfxSlot Slot, LibrarySound? Sound, SfxVariation Variation, bool PitchOverridden, bool GainOverridden);
