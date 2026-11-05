namespace Maxen.DI.Async;

/// <summary>Resolving services that need asynchronous initialization.</summary>
public static class AsyncResolveExtensions
{
    /// <summary>
    /// Resolves a service and, if it implements <see cref="IAsyncInitializable"/>, waits for its
    /// initialization (which runs once per instance). Note that the object you get back is what gets
    /// initialized, so a decorator should forward <c>InitializeAsync</c> to the service it wraps.
    /// </summary>
    public static async Task<T> ResolveAsync<T>(this IScope scope, object? key = null, CancellationToken cancellationToken = default)
        where T : notnull
    {
        var instance = scope.Resolve(new ServiceKey(typeof(T), key));
        await AsyncInitializer.EnsureInitializedAsync(instance, cancellationToken).ConfigureAwait(false);
        return (T)instance;
    }

    /// <summary>
    /// Builds and initializes every singleton that needs async initialization, so the first real
    /// request doesn't pay for it. Services are initialized after the async-initializable
    /// singletons they depend on.
    /// </summary>
    public static async Task InitializeSingletonsAsync(this Container container, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);

        var graph = container.BuildGraph();
        var done = new HashSet<ServiceDescriptor>();

        foreach (var node in graph.Nodes)
            await InitializeAsync(node).ConfigureAwait(false);

        async Task InitializeAsync(ServiceDescriptor node)
        {
            if (!NeedsInitialization(node) || !done.Add(node)) return;

            // Dependencies first: this service's InitializeAsync may well use them.
            foreach (var edge in graph.EdgesFrom(node).Where(e => !e.Deferred))
                await InitializeAsync(edge.To).ConfigureAwait(false);

            var instance = container.Resolve(node.Key);
            await AsyncInitializer.EnsureInitializedAsync(instance, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool NeedsInitialization(ServiceDescriptor node)
        => node.Lifetime == Lifetime.Singleton
            && node.ImplementationType is not null
            && typeof(IAsyncInitializable).IsAssignableFrom(node.ImplementationType);
}
