namespace Olve.Engine3D.Audio.Sfx.Json;

public sealed class NewerSfxLibraryProblem(int version)
    : ResultProblem("The SFX library is version {0}; this game reads versions 1 to {1}", version, SfxLibraryCodec.CurrentVersion);
