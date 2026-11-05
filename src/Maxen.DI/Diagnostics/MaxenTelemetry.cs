using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Maxen.DI.Diagnostics;

/// <summary>
/// The names OpenTelemetry (or any listener) needs to pick up what Maxen.DI reports once
/// <see cref="TelemetryExtensions.UseTelemetry"/> is on: add <see cref="Name"/> as a source and a meter.
/// </summary>
public static class MaxenTelemetry
{
    /// <summary>The name of both the activity source and the meter.</summary>
    public const string Name = "Maxen.DI";

    /// <summary>One span per resolution, nested like the dependency chain.</summary>
    public static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Holds the counter and histogram that resolutions are recorded into.</summary>
    public static Meter Meter { get; } = new(Name);

    internal static Counter<long> Resolutions { get; }
        = Meter.CreateCounter<long>("maxen.di.resolutions", description: "How many resolutions ran.");

    internal static Histogram<double> ResolutionDuration { get; }
        = Meter.CreateHistogram<double>("maxen.di.resolution.duration", unit: "ms", description: "How long resolutions took, dependencies included.");
}
