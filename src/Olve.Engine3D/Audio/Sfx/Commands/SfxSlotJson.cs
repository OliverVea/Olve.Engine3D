using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxSlotJson(Guid Id, string Name, string Bus, Guid? Sound, string? SoundName, SfxVariationJson Variation);
