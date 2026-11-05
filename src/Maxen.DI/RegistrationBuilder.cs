using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>Fluent settings for a single registration, returned by every <c>Register</c> call.</summary>
public sealed class RegistrationBuilder
{
    private readonly List<Type> _additional = new();

    internal RegistrationBuilder(ServiceDescriptor descriptor) => Descriptor = descriptor;

    /// <summary>The registration being configured.</summary>
    public ServiceDescriptor Descriptor { get; }

    /// <summary>Extra service types this registration should also answer to.</summary>
    internal IReadOnlyList<Type> AdditionalServices => _additional;

    /// <summary>A new instance every time (the default).</summary>
    public RegistrationBuilder AsTransient() => WithLifetime(Lifetime.Transient);
    /// <summary>One instance per scope.</summary>
    public RegistrationBuilder AsScoped() => WithLifetime(Lifetime.Scoped);
    /// <summary>One instance for the whole container.</summary>
    public RegistrationBuilder AsSingleton() => WithLifetime(Lifetime.Singleton);

    /// <summary>One instance per scope that carries this label. See <see cref="TaggedScopeLifetime"/>.</summary>
    public RegistrationBuilder AsScopedTo(object tag) => WithLifetime(new TaggedScopeLifetime(tag));

    /// <summary>One instance per scope that is kept for reuse by later scopes. See <see cref="PooledLifetime"/>.</summary>
    public RegistrationBuilder AsPooled(int maxRetained = 16) => WithLifetime(new PooledLifetime(maxRetained));

    /// <summary>One instance per thread. See <see cref="PerThreadLifetime"/>.</summary>
    public RegistrationBuilder AsPerThread() => WithLifetime(new PerThreadLifetime());

    /// <summary>Uses a custom lifetime.</summary>
    public RegistrationBuilder WithLifetime(Lifetime lifetime)
    {
        Descriptor.Lifetime = lifetime;
        return this;
    }

    /// <summary>Makes this a keyed registration. Ask for it with a <see cref="ServiceKey"/> or <c>[FromKey]</c>.</summary>
    public RegistrationBuilder WithKey(object? key)
    {
        Descriptor.Key = new ServiceKey(Descriptor.Key.Type, key);
        return this;
    }

    /// <summary>
    /// Uses this registration only when the check passes. The check gets the service that is asking
    /// (null when the caller asked directly); if it fails, the next most recent registration is used instead.
    /// </summary>
    public RegistrationBuilder When(Func<ResolveContext?, bool> condition)
    {
        Descriptor.Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    /// <summary>Uses this registration only for services being built for <paramref name="consumer"/> (or a subtype of it).</summary>
    public RegistrationBuilder WhenInjectedInto(Type consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        Descriptor.SetMetadata(ServiceMetadata.InjectedInto, consumer);
        return When(asker => asker is not null
            && consumer.IsAssignableFrom(asker.Descriptor.ImplementationType ?? asker.Key.Type));
    }

    /// <summary>Uses this registration only for services being built for <typeparamref name="TConsumer"/>.</summary>
    public RegistrationBuilder WhenInjectedInto<TConsumer>() => WhenInjectedInto(typeof(TConsumer));

    /// <summary>Attaches free-form data to the registration, handy for custom middleware.</summary>
    public RegistrationBuilder WithMetadata(string name, object? value)
    {
        Descriptor.SetMetadata(name, value);
        return this;
    }

    /// <summary>
    /// Also exposes this registration under another service type, sharing the same instance
    /// (so a singleton stays a single object no matter which interface you ask for).
    /// </summary>
    public RegistrationBuilder AlsoAs(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        _additional.Add(serviceType);
        return this;
    }

    /// <summary>Also exposes this registration under <typeparamref name="T"/>, sharing the same instance.</summary>
    public RegistrationBuilder AlsoAs<T>() => AlsoAs(typeof(T));

    /// <summary>
    /// Says that some constructor parameters are supplied at resolve time (see <c>ResolveWith</c> and
    /// <c>Func&lt;TArg, T&gt;</c>). Validation then doesn't complain that they can't be resolved.
    /// </summary>
    public RegistrationBuilder WithRuntimeArguments()
        => WithMetadata(ServiceMetadata.RuntimeArguments, true);

    /// <summary>Tells the container not to dispose what it builds here, because someone else will.</summary>
    public RegistrationBuilder ExternallyOwned()
    {
        Descriptor.OwnsInstance = false;
        return this;
    }

    /// <summary>Adds middleware to this registration only.</summary>
    public RegistrationBuilder Use(MiddlewarePhase phase, IResolutionMiddleware middleware)
    {
        Descriptor.AddMiddleware(phase, middleware);
        return this;
    }

    /// <summary>Adds middleware written as a delegate.</summary>
    public RegistrationBuilder Use(MiddlewarePhase phase, Func<ResolveContext, ResolveDelegate, object> middleware)
        => Use(phase, new DelegateMiddleware(middleware));

    /// <summary>Calls <paramref name="callback"/> each time a new instance has been created (decorators already applied).</summary>
    public RegistrationBuilder OnActivated(Action<object, ResolveContext> callback)
        => Use(MiddlewarePhase.PostLifetime, (ctx, next) =>
        {
            var instance = next(ctx);
            callback(instance, ctx);
            return instance;
        });
}
