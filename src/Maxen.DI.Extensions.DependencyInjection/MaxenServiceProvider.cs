using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>
/// Presents a Maxen scope as a Microsoft <see cref="IServiceProvider"/>, including keyed services
/// and <c>GetRequiredService</c>, which the raw scope doesn't know about.
/// </summary>
/// <remarks>
/// Code written against Microsoft's container expects <see cref="InvalidOperationException"/> when a service can't be
/// built, and some of it (ASP.NET Core included) catches exactly that type. So Maxen's own exceptions are wrapped in one
/// here; the original, with its full explanation, is the <see cref="Exception.InnerException"/>.
/// </remarks>
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

        try
        {
            return Scope.TryResolve(new ServiceKey(serviceType));
        }
        catch (DIException ex)
        {
            throw AsInvalidOperation(ex);
        }
    }

    /// <inheritdoc/>
    public object GetRequiredService(Type serviceType)
    {
        if (serviceType == typeof(IServiceProvider)) return this;

        try
        {
            return Scope.Resolve(new ServiceKey(serviceType));
        }
        catch (ServiceNotRegisteredException ex) when (ex.Key == new ServiceKey(serviceType))
        {
            // Matches what Microsoft's container throws, so existing error handling keeps working.
            throw new InvalidOperationException($"No service for type '{serviceType}' has been registered.", ex);
        }
        catch (DIException ex)
        {
            throw AsInvalidOperation(ex);
        }
    }

    /// <inheritdoc/>
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        try
        {
            return Scope.TryResolve(new ServiceKey(serviceType, KeyMapping.ToMaxen(serviceKey)));
        }
        catch (DIException ex)
        {
            throw AsInvalidOperation(ex);
        }
    }

    /// <inheritdoc/>
    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        var key = new ServiceKey(serviceType, KeyMapping.ToMaxen(serviceKey));
        try
        {
            return Scope.Resolve(key);
        }
        catch (ServiceNotRegisteredException ex) when (ex.Key == key)
        {
            throw new InvalidOperationException(
                $"No service for type '{serviceType}' with key '{serviceKey}' ({serviceKey?.GetType().FullName ?? "null"}) has been registered.", ex);
        }
        catch (DIException ex)
        {
            throw AsInvalidOperation(ex);
        }
    }

    /// <summary>Disposes the wrapped scope.</summary>
    public void Dispose() => Scope.Dispose();

    /// <summary>Asynchronously disposes the wrapped scope.</summary>
    public ValueTask DisposeAsync() => Scope.DisposeAsync();

    // The message stays Maxen's, which already names the service and the path that led to it.
    private static InvalidOperationException AsInvalidOperation(DIException ex) => new(ex.Message, ex);
}
