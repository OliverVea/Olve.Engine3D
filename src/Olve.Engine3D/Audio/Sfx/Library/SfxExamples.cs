using System.Collections.Immutable;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxExamples(ImmutableArray<PatchPanel> Panels, ImmutableArray<LibrarySound> Sounds);
