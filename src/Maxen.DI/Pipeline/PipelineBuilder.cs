using Maxen.DI.Internal;

namespace Maxen.DI.Pipeline;

/// <summary>
/// Assembles the resolution pipeline for a registration, outermost step first:
/// validation, pre-lifetime middleware, lifetime, post-lifetime middleware, decorators, activation.
/// </summary>
internal sealed class PipelineBuilder
{
    private readonly ContainerOptions _options;
    private readonly InstanceFactory _factory;
    private readonly IReadOnlyList<IResolutionMiddleware> _pre;
    private readonly IReadOnlyList<IResolutionMiddleware> _post;
    private readonly IReadOnlyList<DecoratorRegistration> _decorators;

    public PipelineBuilder(
        ContainerOptions options,
        InstanceFactory factory,
        IReadOnlyList<IResolutionMiddleware> pre,
        IReadOnlyList<IResolutionMiddleware> post,
        IReadOnlyList<DecoratorRegistration> decorators)
    {
        _options = options;
        _factory = factory;
        _pre = pre;
        _post = post;
        _decorators = decorators;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Closed generic decorators are built for types the application registers.")]
    public ResolveDelegate Build(ServiceDescriptor descriptor)
    {
        // Innermost step: actually create the object.
        ResolveDelegate activation = ctx => Activate(descriptor, ctx);

        // Steps that only run when something new is created. Global middleware goes first (outermost),
        // then the registration's own, then decorators.
        var postChain = new List<IResolutionMiddleware>(_post);
        postChain.AddRange(descriptor.GetMiddleware(MiddlewarePhase.PostLifetime));

        // Innermost goes first in the chain: lowest priority, and for ties the earliest registered.
        // OrderBy is stable, so equal priorities keep their registration order. We add them in
        // reverse because the chain is wrapped from the end.
        var matching = _decorators.Where(d => d.Matches(descriptor)).OrderBy(d => d.Priority).Reverse();
        foreach (var decorator in matching)
        {
            var decoratorType = decorator.DecoratorType;
            if (decoratorType.IsGenericTypeDefinition)
            {
                decoratorType = OpenGenericCloser.Close(decoratorType, descriptor.Key.Type)
                    ?? throw new RegistrationException(
                        $"Decorator '{TypeNames.Format(decorator.DecoratorType)}' cannot be closed for '{descriptor.Key}'.");
            }
            postChain.Add(new DecoratorMiddleware(descriptor.Key.Type, decoratorType, _factory));
        }

        var created = Wrap(activation, postChain);

        // The lifetime sits between the two chains: it decides whether "created" runs at all.
        var lifetime = descriptor.Lifetime;
        ResolveDelegate lifetimeStep = ctx => lifetime.Resolve(ctx, created);

        var preChain = new List<IResolutionMiddleware>(_pre);
        preChain.AddRange(descriptor.GetMiddleware(MiddlewarePhase.PreLifetime));
        var outer = Wrap(lifetimeStep, preChain);

        // Pre-lifetime middleware is meant to see every resolve (logging, metrics), so only
        // registrations without any can skip the pipeline once their singleton exists.
        var plain = preChain.Count == 0;
        descriptor.CanShortcutCachedInstance = plain && ReferenceEquals(lifetime, Lifetime.Singleton);
        descriptor.CanShortcutScopedInstance = plain && ReferenceEquals(lifetime, Lifetime.Scoped);

        return ctx =>
        {
            Validate(ctx);
            return outer(ctx);
        };
    }

    /// <summary>Wraps <paramref name="inner"/> in each middleware so the first in the list runs first.</summary>
    private static ResolveDelegate Wrap(ResolveDelegate inner, IReadOnlyList<IResolutionMiddleware> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            var middleware = chain[i];
            var next = inner;
            inner = ctx => middleware.Invoke(ctx, next);
        }
        return inner;
    }

    /// <summary>Rejects dependency loops and lifetime mismatches before anything gets built.</summary>
    private void Validate(ResolveContext ctx)
    {
        // Walk the whole chain, not just the direct parent: a singleton that reaches a scoped
        // service through a transient in between still captures it.
        for (var parent = ctx.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent.Descriptor, ctx.Descriptor))
                throw new CircularDependencyException(ctx.DescribePath());

            if (_options.ValidateScopes && !parent.Descriptor.Lifetime.CanDependOn(ctx.Descriptor.Lifetime))
                throw new ScopeValidationException(
                    $"{parent.Descriptor.Lifetime} service '{parent.Key}' cannot depend on " +
                    $"{ctx.Descriptor.Lifetime} service '{ctx.Key}'. Path: {ctx.DescribePath()}");
        }
    }

    private object Activate(ServiceDescriptor descriptor, ResolveContext ctx)
    {
        // Instances come from outside, so we neither construct nor initialize them.
        if (descriptor.Instance is not null) return descriptor.Instance;

        object? instance;
        if (descriptor.Factory is not null)
        {
            instance = descriptor.Factory(ctx);
            if (instance is not null && descriptor.RunInitializers)
                _factory.Initialize(instance, ctx);
        }
        else
        {
            instance = _factory.Create(descriptor.ImplementationType!, ctx, null);
        }

        return instance ?? throw new ResolutionException($"The factory for '{ctx.Key}' returned null.");
    }
}
