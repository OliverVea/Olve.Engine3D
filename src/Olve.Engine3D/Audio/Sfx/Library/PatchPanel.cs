using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record PatchPanel(Id<PatchPanel> Id, string Name, Patch Patch);
