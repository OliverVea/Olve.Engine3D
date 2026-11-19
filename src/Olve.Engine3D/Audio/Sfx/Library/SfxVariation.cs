namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed record SfxVariation(float PitchSemitones, float GainDb)
{
    public static SfxVariation None { get; } = new(0, 0);
}
