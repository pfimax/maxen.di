using Maxen.DI.Pipeline;
using System.Diagnostics;

namespace Maxen.DI.Diagnostics;

/// <summary>Times every resolution and reports it to an observer.</summary>
internal sealed class ObservingMiddleware : IResolutionMiddleware
{
    private readonly IResolutionObserver _observer;
    private long _sequence;

    public ObservingMiddleware(IResolutionObserver observer) => _observer = observer;

    public object Invoke(ResolveContext context, ResolveDelegate next)
    {
        // Numbered when it starts, so a trace can be put back in "parent before child" order
        // even though children finish (and are reported) first.
        var sequence = Interlocked.Increment(ref _sequence);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var instance = next(context);
            Report(context, sequence, started, null);
            return instance;
        }
        catch (Exception ex)
        {
            Report(context, sequence, started, ex);
            throw;
        }
    }

    private void Report(ResolveContext context, long sequence, long started, Exception? error)
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        _observer.OnResolved(new ResolutionEvent(
            sequence, context.Key, context.Descriptor.Lifetime, context.Depth, elapsed, error));
    }
}
