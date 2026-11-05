using Maxen.DI.Arguments;

namespace Maxen.DI;

/// <summary>
/// Something you can resolve services from. A scope owns the scoped instances it creates and
/// disposes them when it is disposed.
/// </summary>
public interface IScope : IServiceProvider, IDisposable, IAsyncDisposable
{
    /// <summary>The container this scope belongs to (the container itself returns itself).</summary>
    IContainer Root { get; }

    /// <summary>True only for the root container.</summary>
    bool IsRoot { get; }

    /// <summary>Instance cache and disposal tracking. Mostly of interest to custom lifetimes.</summary>
    InstanceStore Instances { get; }

    /// <summary>The label this scope was created with (see <see cref="CreateScope(object)"/>), or null.</summary>
    object? Tag { get; }

    /// <summary>Starts a child scope, typically one per request or unit of work.</summary>
    IScope CreateScope();

    /// <summary>
    /// Starts a child scope with a label. Services registered with <c>AsScopedTo(tag)</c> are shared by
    /// everything inside the nearest scope that carries that label, however deeply nested.
    /// </summary>
    IScope CreateScope(object tag);

    /// <summary>True if the service can be resolved from this scope.</summary>
    bool IsRegistered(ServiceKey key);

    /// <summary>Resolves a service, throwing <see cref="ServiceNotRegisteredException"/> if it is unknown.</summary>
    object Resolve(ServiceKey key);

    /// <summary>
    /// Resolves a transient service, giving its constructor values that can't come from registrations.
    /// Throws if the service has any other lifetime.
    /// </summary>
    object ResolveWith(ServiceKey key, ResolveArguments arguments);

    /// <summary>Resolves a service, or returns null if it isn't registered.</summary>
    object? TryResolve(ServiceKey key);

    /// <summary>Resolves every registration of a service, in registration order.</summary>
    IReadOnlyList<object> ResolveAll(ServiceKey key);
}
