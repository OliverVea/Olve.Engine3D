using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxSlot(Id<SfxSlot> Id, string Name, BusName Bus, SfxVariation Variation)
{
    public static SfxSlot Named(string name, BusName bus, SfxVariation variation) =>
        new(Olve.Utilities.Ids.Id.FromName<SfxSlot>(name), name, bus, variation);
}
