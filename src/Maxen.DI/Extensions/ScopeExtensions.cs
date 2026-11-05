using Maxen.DI.Arguments;
using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>Strongly typed shortcuts for resolving services.</summary>
public static class ScopeExtensions
{
    /// <summary>Resolves <typeparamref name="T"/> or throws if it is not registered.</summary>
    public static T Resolve<T>(this IScope scope, object? key = null) where T : notnull
        => (T)scope.Resolve(new ServiceKey(typeof(T), key));

    /// <summary>Resolves the given type or throws if it is not registered.</summary>
    public static object Resolve(this IScope scope, Type type, object? key = null)
        => scope.Resolve(new ServiceKey(type, key));

    /// <summary>
    /// Resolves a transient service and passes values to its constructor by parameter name, e.g.
    /// <c>scope.ResolveWith&lt;Report&gt;(new { title = "Q3" })</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the properties of the arguments object by reflection. Use ResolveArguments to avoid this.")]
    public static T ResolveWith<T>(this IScope scope, object arguments, object? key = null) where T : notnull
        => (T)scope.ResolveWith(new ServiceKey(typeof(T), key), ResolveArguments.FromObject(arguments));

    /// <summary>Resolves a transient service, supplying some constructor values yourself.</summary>
    public static T ResolveWith<T>(this IScope scope, ResolveArguments arguments, object? key = null) where T : notnull
        => (T)scope.ResolveWith(new ServiceKey(typeof(T), key), arguments);

    /// <summary>Returns null instead of throwing when the service isn't registered.</summary>
    public static T? TryResolve<T>(this IScope scope, object? key = null) where T : class
        => (T?)scope.TryResolve(new ServiceKey(typeof(T), key));

    /// <summary>Resolves every registration of <typeparamref name="T"/>, in registration order.</summary>
    public static IReadOnlyList<T> ResolveAll<T>(this IScope scope, object? key = null) where T : notnull
        => scope.ResolveAll(new ServiceKey(typeof(T), key)).Cast<T>().ToList();

    /// <summary>True if <typeparamref name="T"/> can be resolved from this scope.</summary>
    public static bool IsRegistered<T>(this IScope scope, object? key = null)
        => scope.IsRegistered(new ServiceKey(typeof(T), key));

    /// <summary>For use inside factories and middleware, where you resolve dependencies through the context.</summary>
    public static T Resolve<T>(this ResolveContext context, object? key = null) where T : notnull
        => (T)context.Resolve(new ServiceKey(typeof(T), key));
}
