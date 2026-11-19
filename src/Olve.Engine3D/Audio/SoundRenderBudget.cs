namespace Olve.Engine3D.Audio;

public sealed record SoundRenderBudget(TimeSpan PerFrame)
{
    public static readonly SoundRenderBudget RealTime = new(TimeSpan.FromMilliseconds(2));

    public static readonly SoundRenderBudget Whole = new(TimeSpan.MaxValue);
}
