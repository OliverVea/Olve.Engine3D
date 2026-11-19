using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Audio;

namespace Olve.Engine3D.Tests.Audio;

internal sealed class TickingClock(TimeSpan tick) : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks += tick.Ticks;
}

internal static class SoundRendering
{
    public static SoundManager CreateSoundManager(ISoundBackend backend, SoundRenderBudget? budget = null) =>
        new(new TickingClock(SoundRenderBudget.RealTime.PerFrame / 2), budget ?? SoundRenderBudget.RealTime, backend,
            NullLogger<SoundManager>.Instance);

    public static void RenderAll(this SoundManager sounds)
    {
        while (sounds.RenderingCount > 0)
        {
            sounds.Update();
        }
    }
}
