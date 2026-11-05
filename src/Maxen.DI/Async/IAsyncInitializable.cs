namespace Maxen.DI.Async;

/// <summary>
/// For services that need to <c>await</c> something (open a connection, load a cache) before they
/// can be used. Constructors can't be async, so the container offers <c>ResolveAsync</c> and
/// <c>InitializeSingletonsAsync</c>, which call this exactly once per instance.
/// </summary>
public interface IAsyncInitializable
{
    /// <summary>Does the asynchronous start-up work. Called once per instance, after construction, by <c>ResolveAsync</c>.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);
}
