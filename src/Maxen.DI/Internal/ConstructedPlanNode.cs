using Maxen.DI.Pipeline;

namespace Maxen.DI.Internal;

/// <summary>
/// A service built by calling a constructor: how to get it (cached or built), and where each of its constructor
/// arguments comes from. It does the same job as the pipeline for a plain registration, minus the per-dependency
/// context objects.
/// </summary>
internal sealed class ConstructedPlanNode : PlanNode
{
    private readonly ServiceDescriptor _descriptor;
    private readonly Container _owner;
    private readonly ConstructorPlan? _constructor;
    private readonly PlanNode?[] _dependencies;
    private readonly object?[] _defaults;
    private readonly bool _tracksTransient;

    // Only for factories: who is asking, outermost first, so the factory sees a normal dependency path.
    private readonly (Container Owner, ServiceDescriptor Descriptor)[]? _ancestors;

    public ConstructedPlanNode(
        ServiceDescriptor descriptor,
        Container owner,
        ConstructorPlan? constructor,
        PlanNode?[] dependencies,
        object?[] defaults,
        (Container Owner, ServiceDescriptor Descriptor)[]? ancestors = null)
    {
        _ancestors = ancestors;
        _descriptor = descriptor;
        _owner = owner;
        _constructor = constructor;
        _dependencies = dependencies;
        _defaults = defaults;
        _tracksTransient = descriptor.OwnsInstance && ReferenceEquals(descriptor.Lifetime, Lifetime.Transient);
    }

    /// <summary>Gets the service the way its lifetime says: from the cache when there is one, otherwise by building it.</summary>
    public override object Get(IScope scope)
    {
        var lifetime = _descriptor.Lifetime;

        if (ReferenceEquals(lifetime, Lifetime.Singleton))
        {
            // Same rule as the singleton lifetime: build against the container that owns it, never the asking scope.
            var root = _owner;
            if (root.Instances.TryGet(_descriptor, out var known)) return known!;
            return root.Instances.GetOrCreate(_descriptor, (Node: this, Scope: (IScope)root), static s => s.Node.Build(s.Scope), _descriptor.OwnsInstance);
        }

        if (ReferenceEquals(lifetime, Lifetime.Scoped))
        {
            if (scope.Instances.TryGet(_descriptor, out var known)) return known!;
            return scope.Instances.GetOrCreate(_descriptor, (Node: this, Scope: scope), static s => s.Node.Build(s.Scope), _descriptor.OwnsInstance);
        }

        var created = Build(scope);

        // Nobody else knows about a transient, so the current scope takes over disposing it.
        if (_tracksTransient) scope.Instances.Track(created);
        return created;
    }

    private object Build(IScope scope)
    {
        if (_descriptor.Instance is not null) return _descriptor.Instance;

        if (_descriptor.Factory is not null) return BuildWithFactory(scope);

        var plan = _constructor!;

        // Up to four dependencies are passed straight to a compiled call; the array is the general fallback.
        var direct = plan.Fixed;
        if (direct is not null)
        {
            switch (_dependencies.Length)
            {
                case 1: return ((Func<object?, object>)direct)(Argument(0, scope));
                case 2: return ((Func<object?, object?, object>)direct)(Argument(0, scope), Argument(1, scope));
                case 3: return ((Func<object?, object?, object?, object>)direct)(Argument(0, scope), Argument(1, scope), Argument(2, scope));
                case 4: return ((Func<object?, object?, object?, object?, object>)direct)(Argument(0, scope), Argument(1, scope), Argument(2, scope), Argument(3, scope));
            }
        }

        var arguments = _dependencies.Length == 0 ? Array.Empty<object?>() : new object?[_dependencies.Length];
        for (var i = 0; i < arguments.Length; i++)
            arguments[i] = Argument(i, scope);

        return plan.FromArray(arguments);
    }

    // A dependency from the plan, or the parameter's default value when nothing is registered for it.
    private object? Argument(int index, IScope scope) => _dependencies[index]?.Get(scope) ?? _defaults[index];

    private object BuildWithFactory(IScope scope)
    {
        // Rebuild the chain of contexts the pipeline would have made, so ctx.Resolve inside the factory
        // detects loops and lifetime mismatches exactly as it does without a plan.
        ResolveContext? parent = null;
        foreach (var (owner, descriptor) in _ancestors!)
            parent = new ResolveContext(owner, descriptor.Key, descriptor, scope, parent);

        var context = new ResolveContext(_owner, _descriptor.Key, _descriptor, scope, parent);
        return _descriptor.Factory!(context) ?? throw new ResolutionException($"The factory for '{context.Key}' returned null.");
    }
}
