using Maxen.DI.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Maxen.DI;

/// <summary>Writes each resolution to an <see cref="ILogger"/>: failures as errors, slow ones as warnings, the rest at the chosen level.</summary>
internal sealed class LoggingObserver : IResolutionObserver
{
    private readonly ILogger _logger;
    private readonly LogLevel _level;
    private readonly TimeSpan? _slowThreshold;

    public LoggingObserver(ILogger logger, LogLevel level, TimeSpan? slowThreshold)
    {
        _logger = logger;
        _level = level;
        _slowThreshold = slowThreshold;
    }

    public void OnResolved(ResolutionEvent resolution)
    {
        if (resolution.Error is not null)
        {
            _logger.LogError(resolution.Error, "Failed to resolve {Service} ({Lifetime}) after {Milliseconds:F3} ms",
                resolution.Key, resolution.Lifetime.Name, resolution.Duration.TotalMilliseconds);
            return;
        }

        var level = _slowThreshold is { } threshold && resolution.Duration >= threshold ? LogLevel.Warning : _level;
        if (!_logger.IsEnabled(level)) return;

        _logger.Log(level, "Resolved {Service} ({Lifetime}) at depth {Depth} in {Milliseconds:F3} ms",
            resolution.Key, resolution.Lifetime.Name, resolution.Depth, resolution.Duration.TotalMilliseconds);
    }
}
