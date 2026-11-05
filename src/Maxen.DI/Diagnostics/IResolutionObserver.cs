namespace Maxen.DI.Diagnostics;

/// <summary>
/// Gets told about every resolution once it has finished. Use it for logging, metrics or tracing.
/// Called on whichever thread did the resolving, so implementations must be thread-safe.
/// </summary>
public interface IResolutionObserver
{
    /// <summary>Called after a service has been resolved (or has failed to resolve).</summary>
    void OnResolved(ResolutionEvent resolution);
}
