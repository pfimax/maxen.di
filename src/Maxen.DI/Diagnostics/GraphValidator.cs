using Maxen.DI.Internal;

namespace Maxen.DI.Diagnostics;

/// <summary>Looks for loops and lifetime mismatches in a <see cref="DependencyGraph"/>.</summary>
internal static class GraphValidator
{
    public static IReadOnlyList<ValidationIssue> Check(DependencyGraph graph, ContainerOptions options, Container container)
    {
        var issues = new List<ValidationIssue>(graph.ConstructionIssues);
        FindCycles(graph, issues);
        FindUnreachableConditions(graph, container, issues);

        if (options.ValidateScopes)
            FindLifetimeMismatches(graph, issues);

        // Last, and only on request: it runs user code, and it is pointless to run factories of a graph already known to be broken.
        if (options.ValidateFactories && issues.Count == 0)
            RunFactories(graph, container, issues);

        return issues;
    }

    // "Only for ReportJob" is useless if nothing registered is a ReportJob: the registration can never be picked.
    private static void FindUnreachableConditions(DependencyGraph graph, Container container, List<ValidationIssue> issues)
    {
        // Parents count too: a child container can limit a registration to a consumer that lives above it.
        var candidates = graph.Nodes.Concat(container.AllRegistered()).ToList();
        foreach (var node in graph.Nodes)
        {
            if (!node.Metadata.TryGetValue(ServiceMetadata.InjectedInto, out var value) || value is not Type consumer) continue;

            var reachable = candidates.Any(n => consumer.IsAssignableFrom(n.ImplementationType ?? n.Key.Type));
            if (!reachable)
                issues.Add(new ValidationIssue(ValidationIssueKind.UnreachableRegistration, node.Key,
                    $"{node.Key} is limited to '{TypeNames.Format(consumer)}', but nothing registered is one, so it can never be used."));
        }
    }

    // Constructors can be read without running anything, factories cannot. So we call each one and see if it survives.
    private static void RunFactories(DependencyGraph graph, Container container, List<ValidationIssue> issues)
    {
        foreach (var node in graph.Nodes)
        {
            if (node.Factory is null || node.IsSynthetic || node.IsOpenGeneric) continue;

            try
            {
                using var scope = container.CreateScope();
                container.Invoke(node, node.Key, scope, null);
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue(ValidationIssueKind.FactoryFailed, node.Key,
                    $"The factory for {node.Key} threw {ex.GetType().Name}: {ex.Message}"));
            }
        }
    }

    private static void FindCycles(DependencyGraph graph, List<ValidationIssue> issues)
    {
        var finished = new HashSet<ServiceDescriptor>();
        var reported = new HashSet<string>();

        foreach (var start in graph.Nodes)
            Visit(start, new List<ServiceDescriptor>());

        void Visit(ServiceDescriptor node, List<ServiceDescriptor> path)
        {
            var loopStart = path.IndexOf(node);
            if (loopStart >= 0)
            {
                var loop = path.Skip(loopStart).Append(node).ToList();

                // The same loop is reachable from every node in it, so only report it once.
                var signature = string.Join("|", loop.Take(loop.Count - 1).Select(d => d.Key.ToString()).Order());
                if (reported.Add(signature))
                    issues.Add(new ValidationIssue(ValidationIssueKind.CircularDependency, node.Key,
                        "Circular dependency: " + string.Join(" -> ", loop.Select(d => d.Key))));
                return;
            }

            if (finished.Contains(node)) return;

            path.Add(node);
            foreach (var edge in graph.EdgesFrom(node).Where(e => !e.Deferred))
                Visit(edge.To, path);
            path.RemoveAt(path.Count - 1);

            finished.Add(node);
        }
    }

    private static void FindLifetimeMismatches(DependencyGraph graph, List<ValidationIssue> issues)
    {
        var reported = new HashSet<(ServiceDescriptor, ServiceDescriptor)>();

        // A singleton that reaches a scoped service through a transient still captures it, so
        // every service on the way down is checked against every service below it.
        foreach (var start in graph.Nodes)
            Walk(start, new List<ServiceDescriptor>());

        void Walk(ServiceDescriptor node, List<ServiceDescriptor> path)
        {
            if (path.Contains(node)) return; // loops are reported elsewhere

            foreach (var ancestor in path)
            {
                if (ancestor.Lifetime.CanDependOn(node.Lifetime) || !reported.Add((ancestor, node))) continue;

                var chain = path.SkipWhile(d => d != ancestor).Append(node).Select(d => d.Key);
                issues.Add(new ValidationIssue(ValidationIssueKind.LifetimeMismatch, ancestor.Key,
                    $"{ancestor.Lifetime} service '{ancestor.Key}' cannot depend on {node.Lifetime} service '{node.Key}'. " +
                    $"Path: {string.Join(" -> ", chain)}"));
            }

            path.Add(node);
            foreach (var edge in graph.EdgesFrom(node).Where(e => !e.Deferred))
                Walk(edge.To, path);
            path.RemoveAt(path.Count - 1);
        }
    }
}
