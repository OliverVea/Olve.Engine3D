using System.Collections.Immutable;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxLibraryOptions(ImmutableArray<SfxSlot> Slots, SfxExamples Examples);
