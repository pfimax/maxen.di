using Maxen.DI.Arguments;
using System.Collections.Concurrent;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// Makes factories resolvable for any registered T:
/// <c>Func&lt;T&gt;</c> gives a new T per call, and <c>Func&lt;TArg, T&gt;</c> (up to four arguments)
/// passes the arguments to T's constructor, for the parameters that can't come from registrations.
/// The service must be transient to receive arguments.
/// </summary>
public sealed class FuncSource : IRegistrationSource
{
    private const int MaxArguments = 4;

    private static readonly ConcurrentDictionary<Type, Func<IScope, ServiceKey, object>> Makers = new();

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "The Make methods are private to this class and are never trimmed away while it is used.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Func types are closed over types the application already uses, so the code exists.")]
    public IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container)
    {
        if (!IsFunc(key.Type)) yield break;

        // The produced service is always the last type argument: Func<A, B, T> produces T.
        var arguments = key.Type.GetGenericArguments();
        var inner = new ServiceKey(arguments[^1], key.Key);
        if (!container.IsRegistered(inner)) yield break;

        var descriptor = ServiceDescriptor.ForFactory(key.Type, ctx => Create(key.Type, ctx.Scope, inner), key.Key);
        descriptor.RunInitializers = false;
        descriptor.OwnsInstance = false;
        descriptor.IsSynthetic = true;
        yield return descriptor;
    }

    /// <summary>Makes the delegate for a closed <c>Func&lt;..., T&gt;</c> type. Also used by resolve plans, which build the same object without the pipeline.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "The Make methods are private to this class and are never trimmed away while it is used.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Func types are closed over types the application already uses, so the code exists.")]
    internal static object Create(Type funcType, IScope scope, ServiceKey inner)
    {
        var maker = Makers.GetOrAdd(funcType, static type =>
        {
            var typeArguments = type.GetGenericArguments();
            var method = typeof(FuncSource)
                .GetMethod("Make" + (typeArguments.Length - 1), BindingFlags.NonPublic | BindingFlags.Static)!;
            return (Func<IScope, ServiceKey, object>)method
                .MakeGenericMethod(typeArguments)
                .CreateDelegate(typeof(Func<IScope, ServiceKey, object>));
        });
        return maker(scope, inner);
    }

    private static bool IsFunc(Type type)
    {
        if (!type.IsGenericType) return false;

        var count = type.GetGenericArguments().Length;
        return count <= MaxArguments + 1 && type.GetGenericTypeDefinition().FullName == "System.Func`" + count;
    }

    private static object Make0<T>(IScope scope, ServiceKey key)
        => new Func<T>(() => (T)scope.Resolve(key));

    private static object Make1<A, T>(IScope scope, ServiceKey key)
        => new Func<A, T>(a => (T)scope.ResolveWith(key, new ResolveArguments().Add(typeof(A), a)));

    private static object Make2<A, B, T>(IScope scope, ServiceKey key)
        => new Func<A, B, T>((a, b) => (T)scope.ResolveWith(key,
            new ResolveArguments().Add(typeof(A), a).Add(typeof(B), b)));

    private static object Make3<A, B, C, T>(IScope scope, ServiceKey key)
        => new Func<A, B, C, T>((a, b, c) => (T)scope.ResolveWith(key,
            new ResolveArguments().Add(typeof(A), a).Add(typeof(B), b).Add(typeof(C), c)));

    private static object Make4<A, B, C, D, T>(IScope scope, ServiceKey key)
        => new Func<A, B, C, D, T>((a, b, c, d) => (T)scope.ResolveWith(key,
            new ResolveArguments().Add(typeof(A), a).Add(typeof(B), b).Add(typeof(C), c).Add(typeof(D), d)));
}
