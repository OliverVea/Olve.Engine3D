namespace Olve.Engine3D;

/// <summary>
/// One step in turning the frame's <see cref="PlatformEvents"/> into frame state. At the start of each frame,
/// <see cref="GameManager"/> passes the events through every registered stage in <see cref="Order"/>.
/// </summary>
/// <remarks>
/// A stage may return the events changed, so a stage that produces input of its own (e.g. a stick-driven cursor
/// reporting a position) runs before <see cref="PlatformEventStageOrder.Input"/>.
/// </remarks>
public interface IPlatformEventStage
{
    /// <summary>Lower runs first; see <see cref="PlatformEventStageOrder"/>.</summary>
    int Order { get; }

    /// <summary>Applies the frame's events and returns the events for the stages after it.</summary>
    IReadOnlyList<PlatformEvent> Apply(IReadOnlyList<PlatformEvent> events);
}

/// <summary>
/// The engine's stage orders. The window comes first, because input reads the frame's window size.
/// </summary>
public static class PlatformEventStageOrder
{
    public const int Window = 0;
    public const int Input = 1000;
    public const int Audio = 2000;
}
