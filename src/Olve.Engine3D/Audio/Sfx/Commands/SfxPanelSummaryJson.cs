using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxPanelSummaryJson(Guid Id, string Name, IReadOnlyList<SfxInputJson> Inputs, int Sounds);
