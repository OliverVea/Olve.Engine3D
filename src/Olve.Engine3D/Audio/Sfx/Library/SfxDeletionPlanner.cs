using System.Collections.Immutable;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public static class SfxDeletionPlanner
{
    public static Result<SfxDeletion> ForSound(SfxLibraryContent content, Id<LibrarySound> id) =>
        content.Sounds.FirstOrDefault(sound => sound.Id == id) is { } sound
            ? Plan(content, [], [sound])
            : new ResultProblem("There's no sound {0} in the SFX library", id);

    public static Result<SfxDeletion> ForPanel(SfxLibraryContent content, Id<PatchPanel> id) =>
        content.Panels.FirstOrDefault(panel => panel.Id == id) is { } panel
            ? Plan(content, [panel], [.. content.Sounds.Where(sound => sound.Panel == id)])
            : new ResultProblem("There's no panel {0} in the SFX library", id);

    public static SfxLibraryContent Apply(SfxLibraryContent content, SfxDeletion deletion)
    {
        var panels = deletion.Panels.Select(panel => panel.Id).ToHashSet();
        var sounds = deletion.Sounds.Select(sound => sound.Id).ToHashSet();
        return content with
        {
            Panels = content.Panels.RemoveAll(panel => panels.Contains(panel.Id)),
            Sounds = content.Sounds.RemoveAll(sound => sounds.Contains(sound.Id)),
            Bindings = [.. content.Bindings
                .Select(binding => binding.Sound is { } sound && sounds.Contains(sound) ? binding with { Sound = null } : binding)
                .Where(binding => binding.Sound is not null || binding.PitchSemitones is not null || binding.GainDb is not null)],
        };
    }

    private static SfxDeletion Plan(SfxLibraryContent content, ImmutableArray<PatchPanel> panels, ImmutableArray<LibrarySound> sounds)
    {
        var ids = sounds.Select(sound => sound.Id).ToHashSet();
        return new SfxDeletion(panels, sounds, [.. content.Bindings
            .Where(binding => binding.Sound is { } sound && ids.Contains(sound))
            .Select(binding => binding.Slot)]);
    }
}
