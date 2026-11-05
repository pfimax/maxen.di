using Maxen.DI.Pipeline;
using System.Diagnostics;

namespace Maxen.DI.Diagnostics;

/// <summary>Turns every resolution into a span (when something listens) and into counter and histogram readings.</summary>
internal sealed class TelemetryMiddleware : IResolutionMiddleware
{
    private readonly bool _tracing;
    private readonly bool _metrics;

    public TelemetryMiddleware(bool tracing, bool metrics)
    {
        _tracing = tracing;
        _metrics = metrics;
    }

    public object Invoke(ResolveContext context, ResolveDelegate next)
    {
        // StartActivity returns null when nobody listens, so unobserved containers pay almost nothing for spans.
        var activity = _tracing ? MaxenTelemetry.ActivitySource.StartActivity("maxen.di.resolve") : null;
        activity?.SetTag("maxen.service", context.Key.ToString());
        activity?.SetTag("maxen.lifetime", context.Descriptor.Lifetime.Name);
        activity?.SetTag("maxen.depth", context.Depth);

        var started = Stopwatch.GetTimestamp();
        try
        {
            var instance = next(context);
            Record(context, started, "success");
            return instance;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            Record(context, started, "error");
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private void Record(ResolveContext context, long started, string outcome)
    {
        if (!_metrics) return;

        var tags = new KeyValuePair<string, object?>[]
        {
            new("maxen.service", context.Key.ToString()),
            new("maxen.lifetime", context.Descriptor.Lifetime.Name),
            new("outcome", outcome),
        };
        MaxenTelemetry.Resolutions.Add(1, tags);
        MaxenTelemetry.ResolutionDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
    }
}
