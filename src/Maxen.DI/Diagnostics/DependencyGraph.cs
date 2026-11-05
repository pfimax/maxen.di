using Maxen.DI.Pipeline;
using System.Reflection;

namespace Maxen.DI.Diagnostics;

/// <summary>
/// A static picture of "who needs whom", worked out from constructors without building anything.
/// Services created by factories are leaves: the container can't see inside a delegate.
/// Decorators and property injection are not part of the graph.
/// </summary>
public sealed class DependencyGraph
{
    private readonly ILookup<ServiceDescriptor, DependencyEdge> _outgoing;

    private DependencyGraph(
        IReadOnlyList<ServiceDescriptor> nodes,
        IReadOnlyList<DependencyEdge> edges,
        IReadOnlyList<ValidationIssue> constructionIssues)
    {
        Nodes = nodes;
        Edges = edges;
        ConstructionIssues = constructionIssues;
        _outgoing = edges.ToLookup(e => e.From);
    }

    /// <summary>Every registration in the graph.</summary>
    public IReadOnlyList<ServiceDescriptor> Nodes { get; }
    /// <summary>Every "needs" relationship between registrations.</summary>
    public IReadOnlyList<DependencyEdge> Edges { get; }

    /// <summary>Constructors that could not be selected, usually because a dependency is missing.</summary>
    public IReadOnlyList<ValidationIssue> ConstructionIssues { get; }

    /// <summary>The dependencies of one registration.</summary>
    public IEnumerable<DependencyEdge> EdgesFrom(ServiceDescriptor node) => _outgoing[node];

    internal static DependencyGraph Build(Container container)
    {
        var nodes = new List<ServiceDescriptor>();
        var edges = new List<DependencyEdge>();
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

            // Factories, instances and framework built-ins are opaque.
            if (node.ImplementationType is null) continue;

            // A throwaway context lets us reuse the real constructor selection logic.
            var context = new ResolveContext(container, node.Key, node, container, null);

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
                foreach (var (target, deferred) in Targets(container, parameter))
                {
                    Enqueue(target);
                    edges.Add(new DependencyEdge(node, target, deferred));
                }
            }
        }

        return new DependencyGraph(nodes, edges, issues);
    }

    /// <summary>Works out which registrations satisfy one constructor parameter.</summary>
    private static IEnumerable<(ServiceDescriptor Target, bool Deferred)> Targets(Container container, ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        var key = parameter.GetCustomAttribute<FromKeyAttribute>()?.Key;

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
