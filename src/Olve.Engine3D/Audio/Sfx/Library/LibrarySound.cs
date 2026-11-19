using System.Collections.Immutable;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record LibrarySound(
    Id<LibrarySound> Id,
    string Name,
    Id<PatchPanel> Panel,
    TimeSpan Length,
    ImmutableDictionary<Id<PatchInput>, float> Inputs);
