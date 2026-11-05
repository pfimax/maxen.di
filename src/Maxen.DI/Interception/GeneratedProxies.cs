using System.Collections.Concurrent;

namespace Maxen.DI.Interception;

/// <summary>
/// A shelf of ready-made proxy factories. The source generator fills it at startup with one proxy per interface
/// that you intercept in code (<c>InterceptWith</c> / <c>Intercept</c>), so no proxy has to be generated at run time.
/// An interface that isn't on the shelf falls back to a runtime-generated proxy, which native AOT cannot do.
/// </summary>
public static class GeneratedProxies
{
    private static readonly ConcurrentDictionary<Type, Func<object, IReadOnlyList<IInterceptor>, object>> Shelf = new();

    /// <summary>Adds the factory for one interface. Called from generated code; the first one added for a type wins.</summary>
    public static void Add(Type serviceInterface, Func<object, IReadOnlyList<IInterceptor>, object> create)
    {
        ArgumentNullException.ThrowIfNull(serviceInterface);
        ArgumentNullException.ThrowIfNull(create);
        Shelf.TryAdd(serviceInterface, create);
    }

    internal static bool TryGet(Type serviceInterface, out Func<object, IReadOnlyList<IInterceptor>, object> create)
        => Shelf.TryGetValue(serviceInterface, out create!);
}
