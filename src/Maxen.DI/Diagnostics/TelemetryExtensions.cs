using Maxen.DI.Pipeline;

namespace Maxen.DI.Diagnostics;

/// <summary>Switches on OpenTelemetry-style tracing and metrics.</summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Reports every resolution as a span (<see cref="MaxenTelemetry.ActivitySource"/>) and as metrics
    /// (<see cref="MaxenTelemetry.Meter"/>). Nothing is recorded until a listener, such as OpenTelemetry, subscribes
    /// to <see cref="MaxenTelemetry.Name"/>. Like <c>Observe</c>, this sees cached hits too, so singletons lose their
    /// fast path while it is on.
    /// </summary>
    public static ContainerBuilder UseTelemetry(this ContainerBuilder builder, bool tracing = true, bool metrics = true)
        => builder.Use(MiddlewarePhase.PreLifetime, new TelemetryMiddleware(tracing, metrics));
}
