using Olve.Engine3D.Audio.Sfx.Library;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public sealed record SfxVariationJson(float PitchSemitones, float GainDb, bool PitchOverridden, bool GainOverridden);
