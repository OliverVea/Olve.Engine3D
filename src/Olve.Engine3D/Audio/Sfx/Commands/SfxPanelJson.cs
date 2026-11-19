using Olve.Engine3D.Audio.Sfx.Json;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxPanelJson(PanelDocument Panel, IReadOnlyList<Guid> Sounds);
