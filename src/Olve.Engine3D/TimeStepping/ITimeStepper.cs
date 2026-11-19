namespace Olve.Engine3D.TimeStepping;

public interface ITimeStepper : IDisposable
{
    Event Load { get; }
    // TODO: Remove deltaTime from Render — render services don't use it currently.
    // If sim/render rates are decoupled later, accumulate sim deltas for the render delta.
    Event<TimeSpan> Render { get; }
    Event<TimeSpan> Update { get; }
    Event Closing { get; }
    Task RunAsync();
}

