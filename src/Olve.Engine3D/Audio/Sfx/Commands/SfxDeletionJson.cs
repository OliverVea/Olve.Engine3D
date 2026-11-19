using Olve.Engine3D.Audio.Sfx.Library;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxDeletionJson(bool DryRun, IReadOnlyList<string> Panels, IReadOnlyList<string> Sounds, IReadOnlyList<string> UnboundSlots);
