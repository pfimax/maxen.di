using System.Collections.Concurrent;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// Makes <c>Lazy&lt;T&gt;</c> resolvable for any registered T. The real service is only built
/// the first time <c>.Value</c> is read, using the scope that was active when the Lazy was created.
/// </summary>
public sealed class LazySource : IRegistrationSource
{
    // Cache one maker delegate per closed Lazy<T> so we only pay for reflection once.
    private static readonly ConcurrentDictionary<Type, Func<IScope, ServiceKey, object>> Makers = new();

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Lazy types are closed over types the application already uses, so the code exists.")]
    public IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container)
    {
        if (!key.Type.IsGenericType || key.Type.GetGenericTypeDefinition() != typeof(Lazy<>)) yield break;

        // If T isn't registered we offer nothing, so constructors needing Lazy<T> count as unresolvable.
        var inner = new ServiceKey(key.Type.GetGenericArguments()[0], key.Key);
        if (!container.IsRegistered(inner)) yield break;

        var descriptor = ServiceDescriptor.ForFactory(key.Type, ctx => Create(key.Type, ctx.Scope, inner), key.Key);
        descriptor.RunInitializers = false;
        descriptor.OwnsInstance = false;
        descriptor.IsSynthetic = true;
        yield return descriptor;
    }

    /// <summary>Makes the Lazy for a closed <c>Lazy&lt;T&gt;</c> type. Also used by resolve plans, which build the same object without the pipeline.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Lazy types are closed over types the application already uses, so the code exists.")]
    internal static object Create(Type lazyType, IScope scope, ServiceKey inner)
    {
        var maker = Makers.GetOrAdd(lazyType, static type =>
            (Func<IScope, ServiceKey, object>)typeof(LazySource)
                .GetMethod(nameof(Make), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .CreateDelegate(typeof(Func<IScope, ServiceKey, object>)));
        return maker(scope, inner);
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "The container never calls Lazy's parameterless-constructor path; it passes a factory.")]
    private static object Make<T>(IScope scope, ServiceKey key)
        => new Lazy<T>(() => (T)scope.Resolve(key));
}
