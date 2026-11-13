using Maxen.DI.Internal;
using Maxen.DI.Pipeline;
using Maxen.DI.Extensibility;

namespace Maxen.DI;

/// <summary>
/// Where you set everything up. Collect registrations and extensions here, then call
/// <see cref="Build"/> once to get an immutable <see cref="Container"/>.
/// </summary>
public sealed class ContainerBuilder
{
    private readonly List<RegistrationBuilder> _registrations = new();
    private readonly List<IRegistrationSource> _sources = new();
    private readonly List<IResolutionMiddleware> _pre = new();
    private readonly List<IResolutionMiddleware> _post = new();
    private readonly List<IParameterResolver> _parameterResolvers = new();
    private readonly List<IInstanceInitializer> _initializers = new();
    private readonly List<DecoratorRegistration> _decorators = new();
    private readonly Dictionary<string, List<Action<ContainerBuilder>>> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _activeProfiles = new();
    private readonly Container? _parent;
    private bool _built;

    /// <summary>Creates an empty builder.</summary>
    public ContainerBuilder()
    {
    }

    /// <summary>Starts a builder for a child container, inheriting the parent's extension setup.</summary>
    internal ContainerBuilder(Container parent)
    {
        _parent = parent;

        var inherited = parent.Configuration;
        _sources.AddRange(inherited.Sources);
        _pre.AddRange(inherited.PreMiddleware);
        _post.AddRange(inherited.PostMiddleware);
        _parameterResolvers.AddRange(inherited.ParameterResolvers);
        _initializers.AddRange(inherited.Initializers);
        _decorators.AddRange(inherited.Decorators);
        ConstructorSelector = inherited.ConstructorSelector;

        // Every switch starts as the parent has it; the child's own setup can still change any of them.
        Options.ValidateScopes = parent.Options.ValidateScopes;
        Options.ValidateOnBuild = parent.Options.ValidateOnBuild;
        Options.ValidateFactories = parent.Options.ValidateFactories;
        Options.UseResolvePlans = parent.Options.UseResolvePlans;
        Options.DisposeChildScopes = parent.Options.DisposeChildScopes;
    }

    /// <summary>The strictness switches the built container will use.</summary>
    public ContainerOptions Options { get; } = new();

    /// <summary>How constructors get picked. Replace it if the default "greediest wins" rule doesn't suit you.</summary>
    public IConstructorSelector ConstructorSelector { get; set; } = new DefaultConstructorSelector();

    // ---- Registration ------------------------------------------------------------------------

    /// <summary>
    /// Registers an implementation type. Open generics work too:
    /// <c>Register(typeof(IRepo&lt;&gt;), typeof(Repo&lt;&gt;))</c>.
    /// </summary>
    public RegistrationBuilder Register(Type serviceType, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        if (implementationType.IsAbstract || implementationType.IsInterface)
            throw new RegistrationException($"'{TypeNames.Format(implementationType)}' must be a concrete class.");

        if (serviceType.IsGenericTypeDefinition)
        {
            if (!implementationType.IsGenericTypeDefinition || !OpenGenericCloser.Implements(implementationType, serviceType))
                throw new RegistrationException(
                    $"Open generic '{TypeNames.Format(implementationType)}' does not implement '{TypeNames.Format(serviceType)}'.");
        }
        else if (!serviceType.IsAssignableFrom(implementationType))
        {
            throw new RegistrationException(
                $"'{TypeNames.Format(implementationType)}' is not assignable to '{TypeNames.Format(serviceType)}'.");
        }

        return Add(ServiceDescriptor.ForType(serviceType, implementationType));
    }

    /// <summary>Registers a delegate that builds the service.</summary>
    public RegistrationBuilder RegisterFactory(Type serviceType, Func<ResolveContext, object> factory)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(factory);

        // A factory has no way of knowing which closed type it is being asked to produce.
        if (serviceType.IsGenericTypeDefinition)
            throw new RegistrationException("Factories cannot be registered for open generic services.");

        return Add(ServiceDescriptor.ForFactory(serviceType, factory));
    }

    /// <summary>Registers an object you already created. The container will not dispose it.</summary>
    public RegistrationBuilder RegisterInstance(Type serviceType, object instance)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);

        if (!serviceType.IsInstanceOfType(instance))
            throw new RegistrationException($"The instance is not a '{TypeNames.Format(serviceType)}'.");

        return Add(ServiceDescriptor.ForInstance(serviceType, instance));
    }

    /// <summary>Adds a descriptor you built yourself.</summary>
    public RegistrationBuilder Add(ServiceDescriptor descriptor)
    {
        ThrowIfBuilt();
        var registration = new RegistrationBuilder(descriptor);
        _registrations.Add(registration);
        return registration;
    }

    /// <summary>
    /// Drops every registration made so far for exactly this key (open generics only match their own definition).
    /// Returns how many were removed.
    /// </summary>
    public int RemoveAll(ServiceKey key)
    {
        ThrowIfBuilt();
        return _registrations.RemoveAll(r => r.Descriptor.Key == key);
    }

    /// <summary>Checks the registrations made so far. Registration sources are not consulted.</summary>
    public bool IsRegistered(ServiceKey key)
    {
        foreach (var registration in _registrations)
        {
            var registered = registration.Descriptor.Key;
            if (registered == key) return true;

            var coversClosedForm = registered.Type.IsGenericTypeDefinition
                && key.Type.IsConstructedGenericType
                && key.Type.GetGenericTypeDefinition() == registered.Type
                && Equals(registered.Key, key.Key);
            if (coversClosedForm) return true;
        }
        return false;
    }

    /// <summary>
    /// Wraps every registration of <paramref name="serviceType"/> (open generics allowed) in a decorator.
    /// Decorators stack: the first one registered sits closest to the real service, unless
    /// <paramref name="priority"/> says otherwise (lower numbers sit closer to the service).
    /// </summary>
    public ContainerBuilder Decorate(
        Type serviceType, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType, Func<ServiceDescriptor, bool>? when = null, int priority = 0)
    {
        ThrowIfBuilt();

        if (serviceType.IsGenericTypeDefinition != decoratorType.IsGenericTypeDefinition)
            throw new RegistrationException("Service and decorator must both be open generic, or both closed.");

        if (!serviceType.IsGenericTypeDefinition && !serviceType.IsAssignableFrom(decoratorType))
            throw new RegistrationException(
                $"'{TypeNames.Format(decoratorType)}' does not implement '{TypeNames.Format(serviceType)}'.");

        _decorators.Add(new DecoratorRegistration(serviceType, decoratorType, when, priority));
        return this;
    }

    // ---- Extension points --------------------------------------------------------------------

    /// <summary>Applies a reusable bundle of registrations to this builder.</summary>
    public ContainerBuilder RegisterModule(IContainerModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Configure(this);
        return this;
    }

    /// <summary>Adds a source for on-the-fly registrations. Your sources are asked before the built-in ones.</summary>
    public ContainerBuilder AddSource(IRegistrationSource source)
    {
        ThrowIfBuilt();
        _sources.Add(source ?? throw new ArgumentNullException(nameof(source)));
        return this;
    }

    /// <summary>Adds middleware that applies to every registration. The first one added runs outermost.</summary>
    public ContainerBuilder Use(MiddlewarePhase phase, IResolutionMiddleware middleware)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(middleware);

        (phase == MiddlewarePhase.PreLifetime ? _pre : _post).Add(middleware);
        return this;
    }

    /// <summary>Adds a parameter resolver that gets asked before the default one.</summary>
    public ContainerBuilder AddParameterResolver(IParameterResolver resolver)
    {
        ThrowIfBuilt();
        _parameterResolvers.Add(resolver ?? throw new ArgumentNullException(nameof(resolver)));
        return this;
    }

    /// <summary>Adds a step that runs on every newly built instance, such as property injection.</summary>
    public ContainerBuilder AddInitializer(IInstanceInitializer initializer)
    {
        ThrowIfBuilt();
        _initializers.Add(initializer ?? throw new ArgumentNullException(nameof(initializer)));
        return this;
    }

    // ---- Profiles ----------------------------------------------------------------------------

    /// <summary>
    /// Describes a named set of extra registrations, such as "Development" (fakes instead of real
    /// services) or "Tenant-A". Nothing happens until the profile is switched on with <see cref="UseProfile"/>.
    /// Defining the same name twice adds to it.
    /// </summary>
    public ContainerBuilder DefineProfile(string name, Action<ContainerBuilder> configure)
    {
        ThrowIfBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        if (!_profiles.TryGetValue(name, out var actions))
            _profiles[name] = actions = new();
        actions.Add(configure);
        return this;
    }

    /// <summary>
    /// Switches a profile on. Its registrations are applied during <see cref="Build"/>, after everything
    /// registered directly, so they win over the defaults. Profiles apply in the order they were switched on.
    /// </summary>
    public ContainerBuilder UseProfile(string name)
    {
        ThrowIfBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_activeProfiles.Contains(name))
            _activeProfiles.Add(name);
        return this;
    }

    /// <summary>
    /// Switches on the profile named by an environment variable (MAXEN_PROFILE unless you say otherwise),
    /// if it is set. Handy for choosing Development or Production without a code change.
    /// </summary>
    public ContainerBuilder UseProfileFromEnvironment(string variable = "MAXEN_PROFILE")
    {
        var name = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(name) ? this : UseProfile(name);
    }

    /// <summary>The profiles that will be applied at build time, in order.</summary>
    public IReadOnlyList<string> ActiveProfiles => _activeProfiles;

    // ---- Build -------------------------------------------------------------------------------

    /// <summary>Freezes the configuration and creates the container. Can only be called once.</summary>
    public Container Build()
    {
        ThrowIfBuilt();

        // Profiles register more services, which only works while the builder is still open.
        foreach (var name in _activeProfiles)
        {
            if (!_profiles.TryGetValue(name, out var actions))
                throw new RegistrationException(
                    $"Profile '{name}' was switched on but never defined. Defined profiles: " +
                    (_profiles.Count == 0 ? "(none)" : string.Join(", ", _profiles.Keys)) + ".");

            foreach (var action in actions) action(this);
        }

        _built = true;

        var descriptors = new List<ServiceDescriptor>();
        var order = 0;

        // The container can be injected into your own services.
        descriptors.Add(CreateBuiltIn(typeof(IScope), ctx => ctx.Scope, order++));
        descriptors.Add(CreateBuiltIn(typeof(IServiceProvider), ctx => ctx.Scope, order++));
        descriptors.Add(CreateBuiltIn(typeof(IContainer), ctx => ctx.Container, order++));

        foreach (var registration in _registrations)
        {
            var primary = registration.Descriptor;
            primary.Order = order++;
            descriptors.Add(primary);

            foreach (var extraService in registration.AdditionalServices)
                descriptors.Add(CreateForwarder(primary, extraService, order++));
        }

        // Built-in sources go last so yours can override them.
        var sources = new List<IRegistrationSource>(_sources)
        {
            new CollectionSource(),
            new LazySource(),
            new FuncSource(),
        };
        var registry = new ServiceRegistry(descriptors, sources);

        var resolvers = new List<IParameterResolver>(_parameterResolvers) { new DefaultParameterResolver() };
        var factory = new InstanceFactory(ConstructorSelector, resolvers, _initializers.ToArray());

        var decorators = _decorators.ToArray();
        var pre = _pre.ToArray();
        var post = _post.ToArray();

        var configuration = new ContainerConfiguration
        {
            Sources = _sources.ToArray(),
            PreMiddleware = pre,
            PostMiddleware = post,
            ParameterResolvers = _parameterResolvers.ToArray(),
            Initializers = _initializers.ToArray(),
            Decorators = decorators,
            ConstructorSelector = ConstructorSelector,
        };

        var container = new Container(
            registry, Options, factory, configuration, _parent,
            _ => new PipelineBuilder(Options, factory, pre, post, decorators));

        if (Options.ValidateOnBuild)
        {
            try
            {
                container.EnsureValid();
            }
            catch
            {
                // The caller never receives this container, so nobody else can clean it up. With ValidateFactories on,
                // validation may already have built singletons; dispose them here or they are never disposed.
                // The validation error is what the caller needs to see, so a failure while cleaning up doesn't replace it.
                try { container.Dispose(); }
                catch (Exception) { }
                throw;
            }
        }

        return container;
    }

    /// <summary>Creates the registration behind <c>AlsoAs</c>: it simply asks the primary registration for its instance.</summary>
    private static ServiceDescriptor CreateForwarder(ServiceDescriptor primary, Type extraService, int order)
    {
        if (primary.IsOpenGeneric)
            throw new RegistrationException("AlsoAs is not supported for open generic registrations.");

        var actualType = primary.ImplementationType ?? primary.Key.Type;
        if (!extraService.IsAssignableFrom(actualType))
            throw new RegistrationException(
                $"'{TypeNames.Format(actualType)}' is not assignable to '{TypeNames.Format(extraService)}'.");

        // Going through the descriptor directly (instead of by key) keeps this correct even if
        // someone registers the same service again later.
        var primaryKey = primary.Key;
        var forwarder = ServiceDescriptor.ForFactory(extraService, ctx => ctx.ResolveDescriptor(primary, primaryKey), primaryKey.Key);

        // The primary registration already owns and initializes the instance.
        forwarder.OwnsInstance = false;
        forwarder.IsSynthetic = true;
        forwarder.RunInitializers = false;
        forwarder.Order = order;
        return forwarder;
    }

    private static ServiceDescriptor CreateBuiltIn(Type type, Func<ResolveContext, object> factory, int order)
    {
        var descriptor = ServiceDescriptor.ForFactory(type, factory);
        descriptor.OwnsInstance = false;
        descriptor.RunInitializers = false;
        descriptor.IsSynthetic = true;
        descriptor.Order = order;
        return descriptor;
    }

    private void ThrowIfBuilt()
    {
        if (_built)
            throw new InvalidOperationException("The container has already been built.");
    }
}
