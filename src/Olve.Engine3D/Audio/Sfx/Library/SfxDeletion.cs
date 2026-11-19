using System.Collections.Immutable;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxDeletion(
    ImmutableArray<PatchPanel> Panels,
    ImmutableArray<LibrarySound> Sounds,
    ImmutableArray<Id<SfxSlot>> UnboundSlots);
