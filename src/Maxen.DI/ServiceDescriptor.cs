using Maxen.DI.Internal;
using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>
/// Describes how one service gets produced: which key it answers to, what builds it
/// (a type, a factory or a ready-made instance) and how long the result lives.
/// Freely editable until the container is built, read-only afterwards.
/// </summary>
public sealed class ServiceDescriptor
{
    private readonly List<(MiddlewarePhase Phase, IResolutionMiddleware Middleware)> _middleware = new();
    private readonly Dictionary<string, object?> _metadata = new();
    private ServiceKey _key;
    private Lifetime _lifetime = Lifetime.Transient;
    private bool _ownsInstance = true;
    private Func<ResolveContext?, bool>? _condition;
    private bool _frozen;

    private ServiceDescriptor(ServiceKey key, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type? implementationType, Func<ResolveContext, object>? factory, object? instance)
    {
        _key = key;
        ImplementationType = implementationType;
        Factory = factory;
        Instance = instance;

        // A ready-made instance is effectively a singleton, and the caller created it,
        // so the caller (not the container) is responsible for disposing it.
        if (instance is not null)
        {
            _lifetime = Lifetime.Singleton;
            _ownsInstance = false;
        }
    }

    /// <summary>A registration the container will construct by calling a constructor.</summary>
    public static ServiceDescriptor ForType(Type serviceType, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType, object? key = null)
        => new(new ServiceKey(serviceType, key), implementationType, null, null);

    /// <summary>A registration built by your own factory delegate.</summary>
    public static ServiceDescriptor ForFactory(Type serviceType, Func<ResolveContext, object> factory, object? key = null)
        => new(new ServiceKey(serviceType, key), null, factory, null);

    /// <summary>A registration that always hands out the same existing object.</summary>
    public static ServiceDescriptor ForInstance(Type serviceType, object instance, object? key = null)
        => new(new ServiceKey(serviceType, key), null, null, instance);

    /// <summary>The service type (and optional key) this registration answers to.</summary>
    public ServiceKey Key { get => _key; set { Guard(); _key = value; } }

    /// <summary>How long the instances live.</summary>
    public Lifetime Lifetime
    {
        get => _lifetime;
        set { Guard(); _lifetime = value ?? throw new ArgumentNullException(nameof(value)); }
    }

    /// <summary>When true, the container disposes the instances it creates for this registration.</summary>
    public bool OwnsInstance { get => _ownsInstance; set { Guard(); _ownsInstance = value; } }

    /// <summary>The class the container constructs, or null for factories and instances.</summary>
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? ImplementationType { get; }
    /// <summary>The delegate that builds the service, or null for type and instance registrations.</summary>
    public Func<ResolveContext, object>? Factory { get; }
    /// <summary>The ready-made object handed out, or null if the container builds it.</summary>
    public object? Instance { get; }

    /// <summary>True for registrations like <c>IRepo&lt;&gt;</c> that are closed on demand.</summary>
    public bool IsOpenGeneric => _key.Type.IsGenericTypeDefinition;

    /// <summary>
    /// When set, this registration is only used if the check passes for the service that is asking
    /// (null when the caller asked directly). Otherwise the next most recent registration is tried.
    /// </summary>
    public Func<ResolveContext?, bool>? Condition
    {
        get => _condition;
        set { Guard(); _condition = value; }
    }

    /// <summary>Free-form data attached to the registration, handy for custom middleware.</summary>
    public IReadOnlyDictionary<string, object?> Metadata => _metadata;

    /// <summary>Registration order; later registrations win when resolving a single service.</summary>
    internal int Order { get; set; }

    /// <summary>
    /// False for descriptors the framework generates itself (forwarders, collections, ...).
    /// Their results were already initialized once and must not be initialized again.
    /// </summary>
    internal bool RunInitializers { get; set; } = true;

    /// <summary>
    /// True for a plain singleton with nothing running before its lifetime step. Once such an instance
    /// exists, the container can hand it out directly without walking the pipeline again.
    /// </summary>
    internal bool CanShortcutCachedInstance { get; set; }

    /// <summary>
    /// Same idea for a plain scoped service: once the asking scope already holds its instance, the
    /// container can return it without building a context or walking the pipeline.
    /// </summary>
    internal bool CanShortcutScopedInstance { get; set; }

    /// <summary>
    /// True for a closed copy made from an open-generic registration. When an explicit closed registration of the same
    /// service exists it is the more specific one, so it wins a single resolve whatever the registration order.
    /// </summary>
    internal bool IsClosedFromOpenGeneric { get; private set; }

    /// <summary>True for registrations the framework made up itself (built-ins, AlsoAs forwarders), not ones the user wrote.</summary>
    internal bool IsSynthetic { get; set; }

    /// <summary>True when this registration has middleware of its own, which a resolve plan must not skip.</summary>
    internal bool HasMiddleware => _middleware.Count > 0;

    /// <summary>The precomputed recipe for resolving this registration from the top, or <see cref="ResolvePlan.None"/>.</summary>
    internal ResolvePlan? Plan { get; set; }

    /// <summary>The compiled resolution pipeline, built lazily on first use.</summary>
    internal ResolveDelegate? Pipeline { get; set; }

    /// <summary>Attaches free-form data to the registration.</summary>
    public ServiceDescriptor SetMetadata(string name, object? value)
    {
        Guard();
        _metadata[name] = value;
        return this;
    }

    /// <summary>Adds middleware that runs for this registration only.</summary>
    public ServiceDescriptor AddMiddleware(MiddlewarePhase phase, IResolutionMiddleware middleware)
    {
        Guard();
        _middleware.Add((phase, middleware ?? throw new ArgumentNullException(nameof(middleware))));
        return this;
    }

    /// <summary>The middleware registered for one phase, in the order it was added.</summary>
    public IReadOnlyList<IResolutionMiddleware> GetMiddleware(MiddlewarePhase phase)
        => _middleware.Where(m => m.Phase == phase).Select(m => m.Middleware).ToList();

    internal void Freeze() => _frozen = true;

    /// <summary>Makes a closed copy of an open-generic registration, e.g. for <c>IRepo&lt;Customer&gt;</c>.</summary>
    internal ServiceDescriptor Close(Type closedService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type closedImplementation)
    {
        var closed = new ServiceDescriptor(new ServiceKey(closedService, _key.Key), closedImplementation, null, null)
        {
            _lifetime = _lifetime,
            _ownsInstance = _ownsInstance,
            _condition = _condition,
            Order = Order,
            IsClosedFromOpenGeneric = true,
        };
        foreach (var kv in _metadata) closed._metadata[kv.Key] = kv.Value;
        closed._middleware.AddRange(_middleware);
        return closed;
    }

    private void Guard()
    {
        if (_frozen)
            throw new InvalidOperationException("This descriptor belongs to a built container and can no longer be changed.");
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var source = ImplementationType is not null ? TypeNames.Format(ImplementationType)
            : Factory is not null ? "factory" : "instance";
        return $"{_key} -> {source} ({_lifetime})";
    }
}
