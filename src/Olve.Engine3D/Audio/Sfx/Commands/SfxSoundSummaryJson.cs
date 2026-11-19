using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxSoundSummaryJson(Guid Id, string Name, Guid Panel, string PanelName, TimeSpan Length, IReadOnlyList<string> BoundTo);
