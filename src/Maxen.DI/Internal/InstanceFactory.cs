using Maxen.DI.Pipeline;
using System.Collections.Concurrent;
using System.Reflection;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Internal;

/// <summary>
/// Builds objects: picks a constructor, gathers its arguments, calls it through a compiled delegate
/// (much faster than reflection) and finally runs the initializers.
/// </summary>
internal sealed class InstanceFactory
{
    private readonly IConstructorSelector _selector;
    private readonly IReadOnlyList<IParameterResolver> _resolvers;
    private readonly IReadOnlyList<IInstanceInitializer> _initializers;
    private readonly bool _onlyDefaultResolver;

    // Which constructor wins only depends on registrations, which never change after Build(),
    // so it is safe to remember the choice per type.
    private readonly ConcurrentDictionary<Type, ConstructorPlan> _selected = new();
    private readonly ConcurrentDictionary<ConstructorInfo, ConstructorPlan> _plans = new();

    public InstanceFactory(
        IConstructorSelector selector,
        IReadOnlyList<IParameterResolver> resolvers,
        IReadOnlyList<IInstanceInitializer> initializers)
    {
        _selector = selector;
        _resolvers = resolvers;
        _initializers = initializers;
        _onlyDefaultResolver = resolvers.Count == 1 && resolvers[0] is DefaultParameterResolver;
    }

    /// <summary>
    /// Constructs <paramref name="type"/>. <paramref name="overrides"/> holds parameter values the framework
    /// supplies itself (decorators use it to pass in their inner service).
    /// </summary>
    public object Create([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, ResolveContext context, IReadOnlyDictionary<Type, object>? overrides)
    {
        // With overrides or per-call arguments in play, the set of resolvable parameters can differ
        // from call to call, so the cached choice can't be trusted.
        var simple = overrides is null && context.Arguments is null;

        // Only built when something actually needs it; the common path never does.
        ParameterResolutionContext? parameterContext = null;

        ConstructorPlan plan;
        if (simple)
        {
            // Plain read first so the common case doesn't allocate a closure.
            if (!_selected.TryGetValue(type, out plan!))
            {
                parameterContext = new ParameterResolutionContext(context, overrides);
                plan = _selected.GetOrAdd(type, PlanFor(SelectConstructor(type, parameterContext)));
            }
        }
        else
        {
            parameterContext = new ParameterResolutionContext(context, overrides);
            plan = PlanFor(SelectConstructor(type, parameterContext));
        }

        object instance;
        if (_onlyDefaultResolver && simple)
        {
            // Nobody else could claim a parameter, so one compiled call resolves the dependencies and
            // constructs the object: no argument array, no resolver chain.
            instance = plan.Direct(context);
        }
        else
        {
            parameterContext ??= new ParameterResolutionContext(context, overrides);

            var parameters = plan.Parameters;
            var arguments = parameters.Length == 0 ? Array.Empty<object?>() : new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                arguments[i] = ResolveParameter(parameters[i], parameterContext);

            instance = plan.FromArray(arguments);
        }

        Initialize(instance, context);
        return instance;
    }

    /// <summary>
    /// Picks the constructor the normal path would pick for a plain resolve (no overrides, no per-call arguments),
    /// without a resolve context. Returns null if the choice cannot be made that way.
    /// </summary>
    public ConstructorPlan? PlanWithoutBuilding([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, Container container)
    {
        if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters) return null;

        if (_selected.TryGetValue(type, out var known)) return known;

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().All(p => !p.ParameterType.IsByRef && !p.ParameterType.IsPointer))
            .ToList();

        // The default resolver's rule: a parameter is fine if it is registered or has a default value.
        var chosen = _selector.Select(type, constructors,
            p => container.IsRegisteredCore(DefaultParameterResolver.KeyOf(p)) || p.HasDefaultValue);

        return _selected.GetOrAdd(type, PlanFor(chosen));
    }

    private ConstructorPlan PlanFor(ConstructorInfo constructor)
    {
        if (_plans.TryGetValue(constructor, out var plan)) return plan;
        return _plans.GetOrAdd(constructor, new ConstructorPlan(constructor));
    }

    /// <summary>Picks the constructor that would be used, without calling it. Used for validation.</summary>
    public ConstructorInfo Inspect([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, ResolveContext context)
        => SelectConstructor(type, new ParameterResolutionContext(context, null));

    public void Initialize(object instance, ResolveContext context)
    {
        for (var i = 0; i < _initializers.Count; i++)
            _initializers[i].Initialize(instance, context);
    }

    private ConstructorInfo SelectConstructor([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, ParameterResolutionContext parameterContext)
    {
        if (type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
            throw new ResolutionException($"'{TypeNames.Format(type)}' is not a concrete type and cannot be instantiated.");

        // ref/out/pointer parameters can't be satisfied from a container, so skip those constructors.
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().All(p => !p.ParameterType.IsByRef && !p.ParameterType.IsPointer))
            .ToList();

        return _selector.Select(type, constructors, p => CanResolve(p, parameterContext));
    }

    private bool CanResolve(ParameterInfo parameter, ParameterResolutionContext context)
    {
        foreach (var resolver in _resolvers)
            if (resolver.CanResolve(parameter, context)) return true;
        return false;
    }

    private object? ResolveParameter(ParameterInfo parameter, ParameterResolutionContext context)
    {
        foreach (var resolver in _resolvers)
            if (resolver.CanResolve(parameter, context)) return resolver.Resolve(parameter, context);

        throw new ResolutionException(
            $"Cannot resolve parameter '{TypeNames.Format(parameter.ParameterType)} {parameter.Name}'. " +
            $"Path: {context.Context.DescribePath()}");
    }
}
