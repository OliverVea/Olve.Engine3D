using Olve.Engine3D.Audio.Sfx.Library;

namespace Olve.Engine3D.Audio.Sfx.Slots;

internal sealed record RenderedSfx(LibrarySound Sound, PatchPanel Panel, SoundId Id);
