using Maxen.DI.Arguments;
using Maxen.DI.Diagnostics;
using Maxen.DI.Extensibility;
using Maxen.DI.Internal;
using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>The root scope. Create one with <see cref="ContainerBuilder.Build"/>.</summary>
public sealed class Container : Scope, IContainer
{
    private readonly PipelineBuilder _pipelines;

    // The pipeline builder is passed as a callback because it is created by the builder but
    // needs no reference back to the container itself.
    internal Container(
        ServiceRegistry registry,
        ContainerOptions options,
        InstanceFactory factory,
        ContainerConfiguration configuration,
        Container? parent,
        Func<Container, PipelineBuilder> pipelines)
        : base(null)
    {
        Registry = registry;
        Options = options;
        Factory = factory;
        Configuration = configuration;
        Parent = parent;
        _pipelines = pipelines(this);

        // Plans skip the pipeline, so they are only safe when nothing extra hangs off it.
        CanUsePlans = options.UseResolvePlans
            && configuration.PreMiddleware.Count == 0
            && configuration.PostMiddleware.Count == 0
            && configuration.Initializers.Count == 0
            && configuration.Decorators.Count == 0
            && configuration.ParameterResolvers.Count == 0;
    }

    /// <summary>True when this container's setup is plain enough for <see cref="ResolvePlan"/> shortcuts.</summary>
    internal bool CanUsePlans { get; }

    /// <summary>Finds the container (this one or a parent) that owns the winning registration of a key, with that registration.</summary>
    internal (Container Owner, ServiceDescriptor Descriptor)? FindOwnerAndDescriptor(ServiceKey key)
    {
        for (var container = this; container is not null; container = container.ParentCore)
        {
            var descriptor = container.Registry.Find(key, container);
            if (descriptor is not null) return (container, descriptor);
        }
        return null;
    }

    internal ServiceRegistry Registry { get; }
    internal InstanceFactory Factory { get; }
    internal ContainerConfiguration Configuration { get; }

    /// <inheritdoc/>
    public ContainerOptions Options { get; }

    /// <summary>The container this one was created from, or null for a top-level container.</summary>
    public IContainer? Parent { get; }

    private Container? ParentCore => Parent as Container;

    /// <summary>
    /// Creates a child container. Anything the child doesn't register itself is looked up in
    /// this container, and the child's own registrations win. Singletons that belong to this
    /// container stay shared; the child owns (and disposes) only what it creates itself.
    /// Handy for tests, plugins and per-tenant overrides.
    /// </summary>
    public Container CreateChild(Action<ContainerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ContainerBuilder(this);
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Checks the whole configuration without building any service: missing dependencies, loops, lifetime
    /// mismatches and conditional registrations nothing can reach. With <see cref="ContainerOptions.ValidateFactories"/>
    /// on, it also runs each factory once and reports the ones that throw. Returns every problem found (an empty list
    /// means all good).
    /// </summary>
    public IReadOnlyList<ValidationIssue> Validate() => GraphValidator.Check(BuildGraph(), Options, this);

    /// <summary>Like <see cref="Validate"/>, but throws a <see cref="ContainerValidationException"/> if anything is wrong.</summary>
    public void EnsureValid()
    {
        var issues = Validate();
        if (issues.Count > 0) throw new ContainerValidationException(issues);
    }

    /// <summary>Works out who depends on whom, from constructors alone.</summary>
    public DependencyGraph BuildGraph() => DependencyGraph.Build(this);

    /// <summary>True if this container, or any container above it, can supply the service.</summary>
    internal bool IsRegisteredCore(ServiceKey key)
        => Registry.Find(key, this) is not null || ParentCore?.IsRegisteredCore(key) == true;

    /// <summary>The winning registration for a key: ours if we have one, otherwise the parent's.</summary>
    internal ServiceDescriptor? FindDescriptor(ServiceKey key)
        => Registry.Find(key, this) ?? ParentCore?.FindDescriptor(key);

    /// <summary>Like <see cref="FindAllDescriptors"/>, but each registration comes with the container that owns it.</summary>
    internal IEnumerable<(Container Owner, ServiceDescriptor Descriptor)> FindAllOwned(ServiceKey key)
    {
        var inherited = ParentCore?.FindAllOwned(key) ?? Enumerable.Empty<(Container, ServiceDescriptor)>();
        return inherited.Concat(Registry.GetAll(key, this).Select(d => (this, d)));
    }

    /// <summary>Every registration for a key, parents first, so a child's additions come last.</summary>
    internal IEnumerable<ServiceDescriptor> FindAllDescriptors(ServiceKey key)
    {
        var inherited = ParentCore?.FindAllDescriptors(key) ?? Enumerable.Empty<ServiceDescriptor>();
        return inherited.Concat(Registry.GetAll(key, this));
    }

    /// <summary>Resolves the most recent registration of a key.</summary>
    internal object? ResolveCore(
        ServiceKey key, IScope scope, ResolveContext? parent, bool required, ResolveArguments? arguments = null)
    {
        // "Any key" picks out a set of registrations. As a collection that is a fine answer; as one service it would mean
        // guessing which one the caller meant, so it fails even for a "try" resolve rather than quietly returning nothing.
        if (key.IsAnyKey && CollectionSource.ElementType(key.Type) is null)
            throw new ResolutionException(
                $"Cannot resolve a single '{TypeNames.Format(key.Type)}' with ServiceKey.AnyKey: it stands for every key, " +
                $"so there is no one service to return. Resolve IEnumerable<{TypeNames.Format(key.Type)}> with it to get " +
                "every keyed registration, or use a specific key.");

        var descriptor = Registry.Find(key, this);

        // A conditional registration may decline this particular caller; look further back if so.
        if (descriptor?.Condition is not null)
            descriptor = FindApplicable(key, parent);

        if (descriptor is null)
        {
            // Not ours: the parent container gets the same request. It still builds against the
            // scope we pass along, so scoped services land in the right place.
            if (ParentCore is not null)
                return ParentCore.ResolveCore(key, scope, parent, required, arguments);

            if (required)
            {
                // We may be the top of a parent chain that a child started. Suggestions should come from
                // the container the caller actually used, so its own registrations are included too.
                var origin = (scope as Scope)?.Core ?? this;
                throw new ServiceNotRegisteredException(key, parent?.DescribePath(), ServiceSuggestions.For(key, origin.AllRegistered()));
            }
            return null;
        }
        return Invoke(descriptor, key, scope, parent, arguments);
    }

    // Collections, Lazy<T> and Func<T> are plumbing: a condition like "only for ReportJob" should be asked about
    // the ReportJob that wanted the list, not about the list itself. So framework-made askers are looked through.
    private static ResolveContext? RealAsker(ResolveContext? asker)
    {
        while (asker is not null && asker.Descriptor.IsSynthetic) asker = asker.Parent;
        return asker;
    }

    /// <summary>The most recent registration whose condition (if any) accepts the asking service.</summary>
    private ServiceDescriptor? FindApplicable(ServiceKey key, ResolveContext? asker)
    {
        var all = Registry.GetAll(key, this);
        asker = RealAsker(asker);
        for (var i = all.Length - 1; i >= 0; i--)
            if (all[i].Condition is null || all[i].Condition!(asker))
                return all[i];
        return null;
    }

    /// <summary>Every explicit registration here and in the parents. Only used to build helpful error messages.</summary>
    internal IEnumerable<ServiceDescriptor> AllRegistered()
        => Registry.Registered.Concat(ParentCore?.AllRegistered() ?? Enumerable.Empty<ServiceDescriptor>());

    /// <summary>Resolves every registration of a key (inherited ones first), in registration order.</summary>
    internal IReadOnlyList<object> ResolveAllCore(ServiceKey key, IScope scope, ResolveContext? parent)
    {
        var all = Registry.GetAll(key, this);
        var result = new List<object>();
        var asker = RealAsker(parent);

        if (ParentCore is not null)
            result.AddRange(ParentCore.ResolveAllCore(key, scope, parent));

        foreach (var descriptor in all)
        {
            // Same rule as a single resolve: a registration that declines the asker is left out.
            if (descriptor.Condition is not null && !descriptor.Condition(asker)) continue;
            result.Add(Invoke(descriptor, key, scope, parent));
        }

        return result;
    }

    /// <summary>Runs one descriptor through its pipeline, building the pipeline on first use.</summary>
    internal object Invoke(
        ServiceDescriptor descriptor, ServiceKey key, IScope scope, ResolveContext? parent, ResolveArguments? arguments = null)
    {
        // A cached instance can't depend on per-call values, so arguments only make sense for transients.
        if (arguments is not null && descriptor.Lifetime != Lifetime.Transient)
            throw new ResolutionException(
                $"'{key}' is {descriptor.Lifetime}, so it cannot be resolved with arguments. Only transient services can.");

        // Fast path: a singleton that already exists needs no context and no pipeline. A loop can't
        // involve it either, because it only gets cached once fully built. The one check that still
        // matters is that whoever is asking is allowed to hold a singleton.
        if (descriptor.CanShortcutCachedInstance
            && arguments is null
            && CanParentsUse(descriptor, parent)
            && Instances.TryGet(descriptor, out var cached))
            return cached!;

        // Same trick for a scoped service the asking scope already built. A scope that isn't allowed to
        // hold it (the root, with scope validation on) never has an entry, so it still reaches the
        // pipeline and gets its proper error.
        if (descriptor.CanShortcutScopedInstance
            && arguments is null
            && CanParentsUse(descriptor, parent)
            && scope.Instances.TryGet(descriptor, out var scoped))
            return scoped!;

        // A top-level resolve of a plain service can follow a precomputed recipe instead of the pipeline.
        // Anything that needs the pipeline (errors, validation, per-call values) falls through to it.
        if (parent is null && arguments is null)
        {
            var plan = descriptor.Plan ??= ResolvePlanner.Create(this, descriptor);
            if (plan.IsUsable && plan.TryRun(scope, Options.ValidateScopes, out var planned))
                return planned!;
        }

        var context = new ResolveContext(this, key, descriptor, scope, parent, arguments);

        // Two threads may race to build the same pipeline. That's harmless: the results are
        // equivalent and the last write wins.
        var pipeline = descriptor.Pipeline ??= _pipelines.Build(descriptor);
        return pipeline(context);
    }

    /// <summary>Same lifetime rule the pipeline enforces, repeated here so the fast path can't sidestep it.</summary>
    private bool CanParentsUse(ServiceDescriptor descriptor, ResolveContext? parent)
    {
        if (!Options.ValidateScopes) return true;

        for (; parent is not null; parent = parent.Parent)
            if (!parent.Descriptor.Lifetime.CanDependOn(descriptor.Lifetime))
                return false;
        return true;
    }
}
