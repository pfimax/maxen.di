using Maxen.DI.Pipeline;
using System.Collections.Concurrent;

namespace Maxen.DI;

/// <summary>
/// Like <see cref="Lifetime.Scoped"/> (one instance per scope), except that when the scope ends the
/// instance is kept for the next scope instead of being thrown away. Good for objects that are
/// expensive to build, such as buffers or parsers. Implement <see cref="IPoolable"/> to reset state between uses.
/// </summary>
public sealed class PooledLifetime : Lifetime
{
    private readonly int _maxRetained;
    private readonly ConcurrentDictionary<ServiceDescriptor, InstancePool> _pools = new();

    /// <param name="maxRetained">How many idle instances to keep per registration. Extras are disposed.</param>
    public PooledLifetime(int maxRetained = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRetained);
        _maxRetained = maxRetained;
    }

    /// <inheritdoc/>
    public override string Name => "Pooled";

    // A pooled instance is lent to one scope at a time, so a longer-lived service must not hold on to it.
    /// <inheritdoc/>
    public override bool IsScopeBound => true;

    /// <inheritdoc/>
    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        var scope = context.Scope;
        if (scope.IsRoot && context.Container.Options.ValidateScopes)
            throw new ScopeValidationException(
                $"Cannot resolve pooled service '{context.Key}' from the root container. " +
                $"Create a scope first. Path: {context.DescribePath()}");

        // Within one scope the same instance is reused, exactly like a scoped service.
        if (scope.Instances.TryGet(context.Descriptor, out var existing))
            return existing!;

        return scope.Instances.GetOrCreate(context.Descriptor, () =>
        {
            if (!_pools.TryGetValue(context.Descriptor, out var pool))
            {
                // Two scopes can get here together for the first time. Only one pool is kept, and only that one is
                // handed to the container: tracking inside a GetOrAdd callback would also track the loser's, which
                // nothing uses and nothing would release before the container itself is disposed.
                var created = new InstancePool(_maxRetained);
                pool = _pools.GetOrAdd(context.Descriptor, created);

                // Idle instances live as long as the container does.
                if (ReferenceEquals(pool, created))
                    context.Container.Instances.Track(created);
            }

            var instance = pool.Rent() ?? next(context);

            // When the scope ends the lease puts the instance back rather than disposing it.
            scope.Instances.Track(new PoolLease(pool, instance));
            return instance;
        }, owned: false);
    }
}
