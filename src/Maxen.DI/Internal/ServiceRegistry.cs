using System.Collections.Concurrent;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// The read-only lookup of everything that was registered. Answers are worked out the first time a
/// key is asked for, then cached: explicit registrations, merged with closed forms of matching open
/// generics, and only if that yields nothing, whatever the registration sources can offer.
/// </summary>
internal sealed class ServiceRegistry
{
    private readonly Dictionary<ServiceKey, List<ServiceDescriptor>> _explicit = new();
    private readonly List<ServiceDescriptor> _open = new();
    private readonly IReadOnlyList<IRegistrationSource> _sources;
    private readonly ConcurrentDictionary<ServiceKey, ServiceDescriptor[]> _cache = new();

    public ServiceRegistry(IEnumerable<ServiceDescriptor> descriptors, IReadOnlyList<IRegistrationSource> sources)
    {
        _sources = sources;

        foreach (var descriptor in descriptors)
        {
            descriptor.Freeze();

            if (descriptor.IsOpenGeneric)
            {
                _open.Add(descriptor);
                continue;
            }

            if (!_explicit.TryGetValue(descriptor.Key, out var list))
                _explicit[descriptor.Key] = list = new();
            list.Add(descriptor);
        }
    }

    /// <summary>Everything that was explicitly registered (closed registrations and open generics).</summary>
    public IEnumerable<ServiceDescriptor> Registered => _explicit.Values.SelectMany(l => l).Concat(_open);

    /// <summary>Every registration for the key, oldest first.</summary>
    public ServiceDescriptor[] GetAll(ServiceKey key, IContainer container)
    {
        // Hot path: a plain read, so no closure gets allocated for keys we've already worked out.
        if (_cache.TryGetValue(key, out var known))
            return known;

        // GetOrAdd may run the factory on two threads at once, but only one result is stored and
        // every caller gets that one, which matters because scoped/singleton caches key on the descriptor.
        return _cache.GetOrAdd(key, static (k, c) => c.Registry.Materialize(k, c.Container), (Registry: this, Container: container));
    }

    /// <summary>The registration that wins for a key (the most recent one), or null.</summary>
    public ServiceDescriptor? Find(ServiceKey key, IContainer container)
    {
        var all = GetAll(key, container);
        if (all.Length == 0) return null;

        // Usually the last one wins. The exception: a closed registration written out explicitly is more specific
        // than one derived from an open generic, so it is preferred even if the open generic was registered later.
        for (var i = all.Length - 1; i >= 0; i--)
            if (!all[i].IsClosedFromOpenGeneric) return all[i];
        return all[^1];
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Closed generics are only built for types the application registers, so their constructors are preserved.")]
    internal ServiceDescriptor[] Materialize(ServiceKey key, IContainer container)
    {
        var result = new List<ServiceDescriptor>();

        if (_explicit.TryGetValue(key, out var registered))
            result.AddRange(registered);

        // Closed forms of open generics (IRepo<> serving IRepo<Customer>) join the list in registration order.
        if (key.Type.IsConstructedGenericType)
        {
            var definition = key.Type.GetGenericTypeDefinition();
            foreach (var open in _open)
            {
                if (open.Key.Type != definition || !Equals(open.Key.Key, key.Key)) continue;

                var implementation = OpenGenericCloser.Close(open.ImplementationType!, key.Type);
                if (implementation is not null)
                    result.Add(open.Close(key.Type, implementation));
            }
        }

        if (result.Count > 1) result.Sort((a, b) => a.Order.CompareTo(b.Order));
        if (result.Count > 0) return result.ToArray();

        // Nothing registered, so give the sources a chance. The first one with an answer wins.
        foreach (var source in _sources)
        {
            var produced = source.GetRegistrations(key, container).ToList();
            if (produced.Count == 0) continue;

            // Sources are third-party code; a descriptor under the wrong key would be a nasty bug to chase.
            var order = int.MaxValue - produced.Count;
            foreach (var descriptor in produced)
            {
                if (descriptor.Key != key)
                    throw new RegistrationException(
                        $"{source.GetType().Name} returned a descriptor for '{descriptor.Key}' while resolving '{key}'.");

                descriptor.Order = order++;
                descriptor.Freeze();
            }
            return produced.ToArray();
        }

        return Array.Empty<ServiceDescriptor>();
    }
}
