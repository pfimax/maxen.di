using Maxen.DI.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Maxen.DI;

/// <summary>Sends resolution diagnostics to Microsoft.Extensions.Logging.</summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Logs every resolution to <paramref name="logger"/>. Successes use <paramref name="level"/> (Debug by default),
    /// failures are logged as errors, and anything slower than <paramref name="slowThreshold"/> as a warning.
    /// Like <c>Observe</c>, this sees cached hits too, so singletons lose their fast path while it is on.
    /// </summary>
    public static ContainerBuilder LogResolutions(
        this ContainerBuilder builder, ILogger logger, LogLevel level = LogLevel.Debug, TimeSpan? slowThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return builder.Observe(new LoggingObserver(logger, level, slowThreshold));
    }
}
