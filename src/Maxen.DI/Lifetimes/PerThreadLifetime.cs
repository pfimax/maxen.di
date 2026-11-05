using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>
/// One instance per thread. Useful for things that aren't thread-safe but are costly to create
/// (random generators, formatters). Instances are disposed when the container is.
/// </summary>
public sealed class PerThreadLifetime : Lifetime
{
    private readonly ThreadLocal<Dictionary<ServiceDescriptor, object>> _instances = new(() => new());

    /// <inheritdoc/>
    public override string Name => "PerThread";

    // A singleton holding one would silently use the instance of whichever thread built it.
    /// <inheritdoc/>
    public override bool IsScopeBound => true;

    /// <inheritdoc/>
    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        var cache = _instances.Value!;
        if (cache.TryGetValue(context.Descriptor, out var existing))
            return existing;

        var instance = next(context);
        cache[context.Descriptor] = instance;

        // Threads come and go on their own schedule, so the container is the only sensible owner.
        if (context.Descriptor.OwnsInstance)
            context.Container.Instances.Track(instance);

        return instance;
    }
}
