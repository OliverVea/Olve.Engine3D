using System.Collections.Concurrent;

namespace Olve.Engine3D.TimeStepping;

/// <summary>
/// The main thread's synchronization context: an <c>await</c> on the main thread resumes at a fixed point in a later
/// frame (<see cref="RunPosted"/>), wherever the awaited work finished. Work can be posted from any thread.
/// </summary>
public sealed class FrameSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _posted = new();
    private int _mainThreadId;

    /// <summary>Called if a posted callback throws; the frame and the other callbacks go on.</summary>
    public Action<Exception>? OnCallbackException { get; set; }

    public Action? OnPosted { get; set; }

    /// <summary>Makes this the current thread's context; that thread is the one that runs posted work.</summary>
    public void Install()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
        SetSynchronizationContext(this);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _posted.Enqueue((d, state));
        OnPosted?.Invoke();
    }

    /// <summary>Runs inline on the main thread; another thread can't wait for the main thread, as in a browser.</summary>
    public override void Send(SendOrPostCallback d, object? state)
    {
        if (Environment.CurrentManagedThreadId != _mainThreadId)
        {
            throw new NotSupportedException("Waiting for the main thread isn't supported; post instead");
        }

        d(state);
    }

    public override SynchronizationContext CreateCopy() => this;

    /// <summary>
    /// Runs what was posted before this call. Work posted while it runs (an <c>await</c> that resumes and awaits
    /// again, or <c>Task.Yield</c>) waits for the next frame, so a frame always ends.
    /// </summary>
    public void RunPosted()
    {
        for (var count = _posted.Count; count > 0 && _posted.TryDequeue(out var posted); count--)
        {
            try
            {
                posted.Callback(posted.State);
            }
            catch (Exception exception)
            {
                OnCallbackException?.Invoke(exception);
            }
        }
    }
}
