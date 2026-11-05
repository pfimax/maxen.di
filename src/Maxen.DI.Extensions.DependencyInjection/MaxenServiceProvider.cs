using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>
/// Presents a Maxen scope as a Microsoft <see cref="IServiceProvider"/>, including keyed services
/// and <c>GetRequiredService</c>, which the raw scope doesn't know about.
/// </summary>
public sealed class MaxenServiceProvider :
    IServiceProvider, IKeyedServiceProvider, ISupportRequiredService, IDisposable, IAsyncDisposable
{
    /// <summary>Wraps a Maxen scope so Microsoft's APIs can use it as an <see cref="IServiceProvider"/>.</summary>
    public MaxenServiceProvider(IScope scope) => Scope = scope;

    /// <summary>The Maxen scope doing the real work.</summary>
    public IScope Scope { get; }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        // Anyone asking for "the service provider" should get this wrapper, not the bare scope.
        if (serviceType == typeof(IServiceProvider)) return this;

        return Scope.TryResolve(new ServiceKey(serviceType));
    }

    /// <inheritdoc/>
    public object GetRequiredService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider)) return this;

        try
        {
            return Scope.Resolve(new ServiceKey(serviceType));
        }
        catch (ServiceNotRegisteredException ex)
        {
            // Matches what Microsoft's container throws, so existing error handling keeps working.
            throw new InvalidOperationException($"No service for type '{serviceType}' has been registered.", ex);
        }
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
        => Scope.TryResolve(new ServiceKey(serviceType, serviceKey));

    /// <inheritdoc/>
    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        try
        {
            return Scope.Resolve(new ServiceKey(serviceType, serviceKey));
        }
        catch (ServiceNotRegisteredException ex)
        {
            throw new InvalidOperationException(
                $"No service for type '{serviceType}' with key '{serviceKey}' has been registered.", ex);
        }
    }

    /// <summary>Disposes the wrapped scope.</summary>
    public void Dispose() => Scope.Dispose();

    /// <summary>Asynchronously disposes the wrapped scope.</summary>
    public ValueTask DisposeAsync() => Scope.DisposeAsync();
}
