namespace Maxen.DI.Interception;

/// <summary>
/// Wraps calls to a service, for cross-cutting concerns such as logging, caching, retries or timing.
/// Call <see cref="IInvocation.Proceed"/> to let the call through.
/// </summary>
public interface IInterceptor
{
    /// <summary>Handles one call. Call <c>invocation.Proceed()</c> to let it through to the real object.</summary>
    void Intercept(IInvocation invocation);
}
