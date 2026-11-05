namespace Maxen.DI.Diagnostics;

/// <summary>What happened when one service was resolved.</summary>
/// <param name="Sequence">Order in which resolutions started; parents come before their dependencies.</param>
/// <param name="Key">The service that was requested.</param>
/// <param name="Lifetime">The lifetime of the registration that answered.</param>
/// <param name="Depth">How deep in the dependency chain this resolution was (0 = requested directly).</param>
/// <param name="Duration">How long it took, including building its dependencies. Cached instances are fast.</param>
/// <param name="Error">The exception that was thrown, or null on success.</param>
public sealed record ResolutionEvent(
    long Sequence,
    ServiceKey Key,
    Lifetime Lifetime,
    int Depth,
    TimeSpan Duration,
    Exception? Error)
{
    /// <summary>False when the resolve threw.</summary>
    public bool Succeeded => Error is null;
}
