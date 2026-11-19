using System.Collections.Immutable;
using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Validation;

namespace Olve.Engine3D.Audio.Sfx.Library;

public static class SfxLibraryImport
{
    public static Result<SfxImportPlan> Plan(SfxLibraryContent content, ReadOnlySpan<byte> bytes)
    {
        if (SfxLibraryCodec.Decode(bytes).TryPickProblems(out var problems, out var imported))
        {
            return problems.Prepend("Failed to read the SFX library to import");
        }

        if (SfxLibraryValidation.ValidateIds(imported).TryPickProblems(out problems))
        {
            return problems.Prepend("The SFX library to import repeats ids");
        }

        var panels = Merge(content.Panels, imported.Panels, panel => panel.Id, out var panelsAdded, out var panelsReplaced);
        var sounds = Merge(content.Sounds, imported.Sounds, sound => sound.Id, out var soundsAdded, out var soundsReplaced);
        var bindings = Merge(content.Bindings, imported.Bindings, binding => binding.Slot, out var bindingsAdded, out var bindingsReplaced);
        var merged = new SfxLibraryContent(
            panels, sounds, bindings, content.SeededPanels.Union(imported.SeededPanels), content.SeededSounds.Union(imported.SeededSounds));

        if (SfxLibraryValidation.Validate(merged).TryPickProblems(out problems))
        {
            return problems.Prepend("The SFX library to import doesn't fit with this one");
        }

        return new SfxImportPlan(merged, new SfxImport(panelsAdded, panelsReplaced, soundsAdded, soundsReplaced, bindingsAdded, bindingsReplaced));
    }

    private static ImmutableArray<T> Merge<T, TKey>(
        ImmutableArray<T> current, ImmutableArray<T> imported, Func<T, TKey> key, out int added, out int replaced)
        where TKey : notnull
    {
        var byKey = imported.ToDictionary(key);
        replaced = current.Count(item => byKey.ContainsKey(key(item)));
        var existing = current.Select(key).ToHashSet();
        var additions = imported.Where(item => !existing.Contains(key(item))).ToList();
        added = additions.Count;
        return [.. current.Select(item => byKey.GetValueOrDefault(key(item), item)), .. additions];
    }
}
