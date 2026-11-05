using Maxen.DI.Pipeline;

namespace Maxen.DI.Diagnostics;

/// <summary>Switches on resolution diagnostics.</summary>
public static class DiagnosticsExtensions
{
    /// <summary>Reports every resolution, with timing, to <paramref name="observer"/>.</summary>
    public static ContainerBuilder Observe(this ContainerBuilder builder, IResolutionObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        // Pre-lifetime so cached hits are seen too, not just the ones that built something.
        return builder.Use(MiddlewarePhase.PreLifetime, new ObservingMiddleware(observer));
    }

    /// <summary>Calls <paramref name="action"/> after every resolve, successful or not.</summary>
    public static ContainerBuilder Observe(this ContainerBuilder builder, Action<ResolutionEvent> action)
        => builder.Observe(new DelegateResolutionObserver(action));

    /// <summary>Calls <paramref name="onSlow"/> for any resolution that takes at least <paramref name="threshold"/>.</summary>
    public static ContainerBuilder ObserveSlowResolutions(
        this ContainerBuilder builder, TimeSpan threshold, Action<ResolutionEvent> onSlow)
        => builder.Observe(e =>
        {
            if (e.Duration >= threshold) onSlow(e);
        });
}
