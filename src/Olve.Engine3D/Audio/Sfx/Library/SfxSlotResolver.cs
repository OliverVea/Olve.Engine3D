namespace Olve.Engine3D.Audio.Sfx.Library;

public static class SfxSlotResolver
{
    public static SfxSlotState Resolve(SfxSlot slot, SfxLibraryContent content)
    {
        var binding = content.Bindings.FirstOrDefault(binding => binding.Slot == slot.Id);
        var sound = binding?.Sound is { } id ? content.Sounds.FirstOrDefault(sound => sound.Id == id) : null;
        var variation = new SfxVariation(
            binding?.PitchSemitones ?? slot.Variation.PitchSemitones,
            binding?.GainDb ?? slot.Variation.GainDb);
        return new SfxSlotState(slot, sound, variation, binding?.PitchSemitones is not null, binding?.GainDb is not null);
    }
}
