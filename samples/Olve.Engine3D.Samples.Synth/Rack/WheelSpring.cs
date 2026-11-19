namespace Olve.Engine3D.Samples.Synth.Rack;

/// <summary>
/// The pitch wheel's rubbery spring back to its middle once let go: a damped spring that overshoots the middle a little
/// and settles. Each step is solved exactly, so a long frame lands where the spring would be, never past it.
/// </summary>
public static class WheelSpring
{
    /// <summary>How fast it swings, in cycles a second, and how soon the swing dies, from 0 (never) to 1 (no overshoot).</summary>
    public const float Frequency = 3, Damping = 0.5f;

    // Closer than this to the middle, and slower, it has settled
    private const float Settled = 0.002f, SettledSpeed = 0.02f;

    /// <summary>The wheel's turn from its middle and its speed, <paramref name="seconds"/> later; exactly the middle once settled.</summary>
    public static (float Turn, float Speed) Step(float turn, float speed, float seconds)
    {
        var omega = 2 * MathF.PI * Frequency;
        var decay = Damping * omega;
        var swing = omega * MathF.Sqrt(1 - Damping * Damping);
        var fade = MathF.Exp(-decay * seconds);
        var (cos, sin) = (MathF.Cos(swing * seconds), MathF.Sin(swing * seconds));
        var next = fade * (turn * cos + (speed + decay * turn) / swing * sin);
        var nextSpeed = fade * (speed * cos - (omega * omega * turn + decay * speed) / swing * sin);
        return MathF.Abs(next) < Settled && MathF.Abs(nextSpeed) < SettledSpeed ? (0, 0) : (next, nextSpeed);
    }
}
