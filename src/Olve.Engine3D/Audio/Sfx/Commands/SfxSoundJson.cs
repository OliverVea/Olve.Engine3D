using Olve.Engine3D.Audio.Sfx.Json;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxSoundJson(SoundDocument Sound, string PanelName, IReadOnlyList<string> BoundTo);
