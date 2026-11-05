namespace Maxen.DI.Pipeline;

/// <summary>
/// A hook into the resolution pipeline. Call <c>next</c> to continue, or return something
/// else to short-circuit (a cached value, a proxy, a mock).
/// </summary>
public interface IResolutionMiddleware
{
    /// <summary>Runs around a resolve. Call <paramref name="next"/> to continue down the pipeline.</summary>
    object Invoke(ResolveContext context, ResolveDelegate next);
}
