namespace Maxen.DI.Extensibility;

/// <summary>
/// Makes <c>IEnumerable&lt;T&gt;</c>, <c>T[]</c> and friends resolvable as "every registration of T".
/// An empty collection is a valid answer, so asking for a list of something nobody registered never fails.
/// </summary>
public sealed class CollectionSource : IRegistrationSource
{
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Arrays of reference types and of types the application already uses are available ahead of time.")]
    public IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container)
    {
        var element = ElementType(key.Type);
        if (element is null) yield break;

        // A keyed collection gathers the services registered under that same key.
        var elementKey = new ServiceKey(element, key.Key);

        var descriptor = ServiceDescriptor.ForFactory(key.Type, ctx =>
        {
            var items = ctx.ResolveAll(elementKey);

            // An array of the exact element type is assignable to every collection interface we support.
            var array = Array.CreateInstance(element, items.Count);
            for (var i = 0; i < items.Count; i++)
                array.SetValue(items[i], i);
            return array;
        }, key.Key);

        descriptor.RunInitializers = false;
        descriptor.OwnsInstance = false;
        descriptor.IsSynthetic = true;

        // A read-only list of singletons never changes, so build it once and hand out the same one, as Microsoft's
        // container does. Arrays, IList<T> and ICollection<T> invite changes, so those stay a fresh copy per resolve.
        if (IsReadOnlyShape(key.Type) && container is Container owner && OnlySingletons(owner, elementKey))
            descriptor.Lifetime = Lifetime.Singleton;

        yield return descriptor;
    }

    private static bool IsReadOnlyShape(Type type)
    {
        if (!type.IsGenericType) return false;
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(IEnumerable<>)
            || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(IReadOnlyList<>);
    }

    // A condition can include or leave out a member depending on who asks, so a list with one can't be shared.
    private static bool OnlySingletons(Container container, ServiceKey elementKey)
        => container.FindAllDescriptors(elementKey)
            .All(d => ReferenceEquals(d.Lifetime, Lifetime.Singleton) && d.Condition is null);

    /// <summary>The element type if <paramref name="type"/> is a collection shape this source supplies, otherwise null.</summary>
    internal static Type? ElementType(Type type)
    {
        if (type.IsArray && type.GetArrayRank() == 1)
            return type.GetElementType();

        if (!type.IsGenericType) return null;

        var definition = type.GetGenericTypeDefinition();
        var isSupported = definition == typeof(IEnumerable<>)
            || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(IReadOnlyList<>)
            || definition == typeof(ICollection<>)
            || definition == typeof(IList<>);

        return isSupported ? type.GetGenericArguments()[0] : null;
    }
}
