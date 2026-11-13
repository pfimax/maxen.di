using Maxen.DI.Internal;
using Maxen.DI.Pipeline;
using System.Reflection;

namespace Maxen.DI.Diagnostics;

/// <summary>
/// A static picture of "who needs whom", worked out from constructors without building anything.
/// Services created by factories are leaves: the container can't see inside a delegate.
/// Decorators and property injection are not part of the graph. What a decorator needs is still worked out, and kept
/// to one side for validation, so a decorator with a missing or too short-lived dependency is found.
/// </summary>
public sealed class DependencyGraph
{
    private readonly ILookup<ServiceDescriptor, DependencyEdge> _outgoing;
    private readonly ILookup<ServiceDescriptor, DependencyEdge> _decoratorNeeds;

    private DependencyGraph(
        IReadOnlyList<ServiceDescriptor> nodes,
        IReadOnlyList<DependencyEdge> edges,
        IReadOnlyList<DependencyEdge> decoratorEdges,
        IReadOnlyList<ValidationIssue> constructionIssues)
    {
        Nodes = nodes;
        Edges = edges;
        ConstructionIssues = constructionIssues;
        _outgoing = edges.ToLookup(e => e.From);
        _decoratorNeeds = decoratorEdges.ToLookup(e => e.From);
    }

    /// <summary>Every registration in the graph.</summary>
    public IReadOnlyList<ServiceDescriptor> Nodes { get; }
    /// <summary>Every "needs" relationship between registrations.</summary>
    public IReadOnlyList<DependencyEdge> Edges { get; }

    /// <summary>Constructors that could not be selected, usually because a dependency is missing.</summary>
    public IReadOnlyList<ValidationIssue> ConstructionIssues { get; }

    /// <summary>The dependencies of one registration.</summary>
    public IEnumerable<DependencyEdge> EdgesFrom(ServiceDescriptor node) => _outgoing[node];

    /// <summary>
    /// Everything building one registration pulls in: its own dependencies and those of the decorators wrapped around
    /// it. A decorator lives exactly as long as the service it wraps, so for loops and lifetimes its dependencies count
    /// as the service's own. They stay out of <see cref="Edges"/>, which is what diagrams and snapshots are made from.
    /// </summary>
    internal IEnumerable<DependencyEdge> NeedsOf(ServiceDescriptor node) => _outgoing[node].Concat(_decoratorNeeds[node]);

    internal static DependencyGraph Build(Container container)
    {
        var nodes = new List<ServiceDescriptor>();
        var edges = new List<DependencyEdge>();
        var decoratorEdges = new List<DependencyEdge>();
        var issues = new List<ValidationIssue>();

        // Work through a queue so closed forms of open generics we only discover through an
        // edge (IRepo<Customer>) get analyzed too.
        var seen = new HashSet<ServiceDescriptor>();
        var queue = new Queue<ServiceDescriptor>();

        void Enqueue(ServiceDescriptor descriptor)
        {
            if (seen.Add(descriptor)) queue.Enqueue(descriptor);
        }

        foreach (var descriptor in container.Registry.Registered.Where(d => !d.IsOpenGeneric))
            Enqueue(descriptor);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            nodes.Add(node);

            // A throwaway context lets us reuse the real constructor selection logic.
            var context = new ResolveContext(container, node.Key, node, container, null);

            // Decorators wrap whatever the registration produces, so this comes before the "opaque" exit below.
            foreach (var (target, deferred) in DecoratorNeeds(container, node, context, issues))
            {
                Enqueue(target);
                decoratorEdges.Add(new DependencyEdge(node, target, deferred));
            }

            // Factories, instances and framework built-ins are opaque, unless the registration says what it resolves.
            if (node.ImplementationType is null)
            {
                foreach (var (target, deferred) in DeclaredNeeds(container, node, issues))
                {
                    Enqueue(target);
                    edges.Add(new DependencyEdge(node, target, deferred));
                }
                continue;
            }

            ConstructorInfo constructor;
            try
            {
                constructor = container.Factory.Inspect(node.ImplementationType, context);
            }
            catch (ResolutionException ex)
            {
                // Services that take values at resolve time can't be checked statically; trust the developer.
                if (node.Metadata.ContainsKey(ServiceMetadata.RuntimeArguments)) continue;

                issues.Add(new ValidationIssue(ValidationIssueKind.MissingDependency, node.Key, $"{node.Key}: {ex.Message}"));
                continue;
            }

            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var (target, deferred) in Targets(container, parameter, node.Key.Key))
                {
                    Enqueue(target);
                    edges.Add(new DependencyEdge(node, target, deferred));
                }
            }
        }

        return new DependencyGraph(nodes, edges, decoratorEdges, issues);
    }

    /// <summary>
    /// What the decorators around one registration need, apart from the service they wrap. A decorator that can't be
    /// built at all is added to <paramref name="issues"/>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Closed generic decorators are built for types the application registers.")]
    private static List<(ServiceDescriptor Target, bool Deferred)> DecoratorNeeds(
        Container container, ServiceDescriptor node, ResolveContext context, List<ValidationIssue> issues)
    {
        var needs = new List<(ServiceDescriptor, bool)>();

        var decorators = container.Configuration.Decorators;
        if (decorators.Count == 0) return needs;

        // A registration that belongs to a parent container is built there, with the parent's decorators, and is
        // checked when the parent is validated.
        if (Array.IndexOf(container.Registry.GetAll(node.Key, container), node) < 0) return needs;

        var serviceType = node.Key.Type;

        // Only says "this parameter is taken care of"; the value is never read while inspecting.
        var inner = new Dictionary<Type, object> { [serviceType] = node };

        foreach (var decorator in decorators)
        {
            if (!decorator.Matches(node)) continue;

            var decoratorType = decorator.DecoratorType;
            if (decoratorType.IsGenericTypeDefinition)
            {
                var closed = OpenGenericCloser.Close(decoratorType, serviceType);
                if (closed is null)
                {
                    issues.Add(new ValidationIssue(ValidationIssueKind.MissingDependency, node.Key,
                        $"{node.Key}: decorator '{TypeNames.Format(decoratorType)}' cannot be closed for it."));
                    continue;
                }
                decoratorType = closed;
            }

            ConstructorInfo constructor;
            try
            {
                constructor = container.Factory.Inspect(decoratorType, context, inner);
            }
            catch (ResolutionException ex)
            {
                issues.Add(new ValidationIssue(ValidationIssueKind.MissingDependency, node.Key,
                    $"{node.Key}: its decorator cannot be built. {ex.Message}"));
                continue;
            }

            foreach (var parameter in constructor.GetParameters())
            {
                if (parameter.ParameterType == serviceType) continue;
                needs.AddRange(Targets(container, parameter, node.Key.Key));
            }
        }

        return needs;
    }

    /// <summary>
    /// The dependencies a factory registration lists under <see cref="ServiceMetadata.Dependencies"/>, which is how the
    /// source generator tells us what the registrations it writes resolve. One that nothing can supply is added to
    /// <paramref name="issues"/>, as an unresolvable constructor parameter would be.
    /// </summary>
    private static List<(ServiceDescriptor Target, bool Deferred)> DeclaredNeeds(
        Container container, ServiceDescriptor node, List<ValidationIssue> issues)
    {
        var needs = new List<(ServiceDescriptor, bool)>();
        if (!node.Metadata.TryGetValue(ServiceMetadata.Dependencies, out var value) || value is not IEnumerable<ServiceKey> declared)
            return needs;

        foreach (var dependency in declared)
        {
            var key = ReferenceEquals(dependency.Key, ServiceMetadata.SameKey) ? node.Key.Key : dependency.Key;

            // As with a constructor: under an any-key registration the real key isn't known until someone asks.
            if (ReferenceEquals(key, ServiceKey.AnyKey)) continue;

            var before = needs.Count;
            needs.AddRange(TargetsFor(container, dependency.Type, key));

            // A collection may be empty; anything else has to be there.
            if (needs.Count == before && CollectionElement(dependency.Type) is null)
                issues.Add(new ValidationIssue(ValidationIssueKind.MissingDependency, node.Key,
                    $"{node.Key}: its factory resolves '{new ServiceKey(dependency.Type, key)}', which is not registered."));
        }

        return needs;
    }

    /// <summary>Works out which registrations satisfy one constructor parameter.</summary>
    private static IEnumerable<(ServiceDescriptor Target, bool Deferred)> Targets(Container container, ParameterInfo parameter, object? ownerKey)
    {
        var declared = ParameterKeys.Of(parameter);

        // [ServiceKey] receives a value, not a service, so there is nothing to point at.
        if (ParameterKeys.IsOwnKey(declared)) return Enumerable.Empty<(ServiceDescriptor, bool)>();

        var key = ParameterKeys.ForOwner(declared, ownerKey).Key;

        // An any-key registration inherits whatever key it is asked with, which isn't known until then.
        if (ReferenceEquals(key, ServiceKey.AnyKey)) return Enumerable.Empty<(ServiceDescriptor, bool)>();

        return TargetsFor(container, parameter.ParameterType, key);
    }

    /// <summary>Works out which registrations satisfy a request for one service type under one key.</summary>
    private static IEnumerable<(ServiceDescriptor Target, bool Deferred)> TargetsFor(Container container, Type type, object? key)
    {

        // Collections depend on every registration of the element type.
        var element = CollectionElement(type);
        if (element is not null)
        {
            foreach (var d in container.FindAllDescriptors(new ServiceKey(element, key)))
                yield return (d, false);
            yield break;
        }

        // Lazy<T> and Func<..., T> only resolve T later.
        var deferredTarget = DeferredTarget(type);
        if (deferredTarget is not null)
        {
            var d = container.FindDescriptor(new ServiceKey(deferredTarget, key));
            if (d is not null) yield return (d, true);
            yield break;
        }

        var direct = container.FindDescriptor(new ServiceKey(type, key));
        if (direct is not null) yield return (direct, false);
    }

    private static Type? CollectionElement(Type type)
    {
        if (type.IsArray && type.GetArrayRank() == 1) return type.GetElementType();
        if (!type.IsGenericType) return null;

        var definition = type.GetGenericTypeDefinition();
        var isCollection = definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyCollection<>)
            || definition == typeof(IReadOnlyList<>) || definition == typeof(ICollection<>) || definition == typeof(IList<>);
        return isCollection ? type.GetGenericArguments()[0] : null;
    }

    private static Type? DeferredTarget(Type type)
    {
        if (!type.IsGenericType) return null;

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(Lazy<>)) return type.GetGenericArguments()[0];

        // Func<T>, Func<A, T>, ...: the produced service is always the last type argument.
        var isFunc = definition.FullName?.StartsWith("System.Func`", StringComparison.Ordinal) == true;
        return isFunc ? type.GetGenericArguments()[^1] : null;
    }
}
