using System.Collections.Immutable;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxLibraryContent(
    ImmutableArray<PatchPanel> Panels,
    ImmutableArray<LibrarySound> Sounds,
    ImmutableArray<SfxBinding> Bindings,
    ImmutableHashSet<Id<PatchPanel>> SeededPanels,
    ImmutableHashSet<Id<LibrarySound>> SeededSounds)
{
    public static SfxLibraryContent Empty { get; } = new([], [], [], [], []);
}
