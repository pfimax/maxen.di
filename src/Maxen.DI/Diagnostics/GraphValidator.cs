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
        // A service registered with AsScopedTo("request") can only be built inside a scope carrying that label, and a
        // factory may ask for one whatever its own lifetime is. So the throwaway scope sits inside one scope per label
        // in use; without them a healthy factory would be reported as failing for want of a scope.
        var labels = container.AllRegistered()
            .Select(d => d.Lifetime)
            .OfType<TaggedScopeLifetime>()
            .Select(l => l.Tag)
            .Distinct()
            .ToList();

        foreach (var node in graph.Nodes)
        {
            if (node.Factory is null || node.IsSynthetic || node.IsOpenGeneric) continue;

            try
            {
                var scopes = new List<IScope>(labels.Count + 1);
                try
                {
                    IScope scope = container;
                    foreach (var label in labels)
                        scopes.Add(scope = scope.CreateScope(label));
                    scopes.Add(scope = scope.CreateScope());

                    container.Invoke(node, node.Key, scope, null);
                }
                finally
                {
                    // Innermost first, the order they would end in a real application.
                    DisposeAll(scopes);
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue(ValidationIssueKind.FactoryFailed, node.Key,
                    $"The factory for {node.Key} threw {ex.GetType().Name}: {ex.Message}"));
            }
        }
    }

    // Disposes every scope even if one of them throws, then reports what went wrong.
    private static void DisposeAll(List<IScope> scopes)
    {
        List<Exception>? errors = null;
        for (var i = scopes.Count - 1; i >= 0; i--)
        {
            try { scopes[i].Dispose(); }
            catch (Exception ex) { (errors ??= new()).Add(ex); }
        }

        if (errors is not null)
            throw errors.Count == 1 ? errors[0] : new AggregateException(errors);
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
            foreach (var edge in graph.NeedsOf(node).Where(e => !e.Deferred))
                Visit(edge.To, path);
            path.RemoveAt(path.Count - 1);

            finished.Add(node);
        }
    }

    private static void FindLifetimeMismatches(DependencyGraph graph, List<ValidationIssue> issues)
    {
        var reported = new HashSet<(ServiceDescriptor, ServiceDescriptor)>();

        // Most services may depend on anything (a transient, a scoped service). Only the ones whose lifetime turns
        // something in this graph down can ever be at fault, so only those are remembered on the way down.
        var lifetimes = graph.Nodes.Select(n => n.Lifetime).Distinct().ToList();
        var restrictive = new Dictionary<Lifetime, bool>(ReferenceEqualityComparer.Instance);

        bool IsRestrictive(Lifetime lifetime)
        {
            if (!restrictive.TryGetValue(lifetime, out var answer))
                restrictive[lifetime] = answer = lifetimes.Any(other => !lifetime.CanDependOn(other));
            return answer;
        }

        // For each service, the restrictive services it has already been walked below. A service many others depend
        // on is reached by many paths, and walking everything below it again for each one makes the work grow with
        // the number of paths, which doubles per level in a graph of shared services. A walk can only find something
        // new for a holder that hasn't been above this service before, so the others are skipped.
        var walkedBelow = new Dictionary<ServiceDescriptor, HashSet<ServiceDescriptor>>();

        var path = new List<ServiceDescriptor>();
        var onPath = new HashSet<ServiceDescriptor>();
        var holders = new List<ServiceDescriptor>();

        // A singleton that reaches a scoped service through a transient still captures it, so
        // every service on the way down is checked against every service below it.
        foreach (var start in graph.Nodes)
            Walk(start);

        void Walk(ServiceDescriptor node)
        {
            if (onPath.Contains(node)) return; // loops are reported elsewhere

            if (walkedBelow.TryGetValue(node, out var seen))
            {
                if (holders.TrueForAll(seen.Contains)) return;
            }
            else
            {
                walkedBelow[node] = seen = new HashSet<ServiceDescriptor>();
            }
            seen.UnionWith(holders);

            foreach (var holder in holders)
            {
                if (holder.Lifetime.CanDependOn(node.Lifetime) || !reported.Add((holder, node))) continue;

                var chain = path.SkipWhile(d => d != holder).Append(node).Select(d => d.Key);
                issues.Add(new ValidationIssue(ValidationIssueKind.LifetimeMismatch, holder.Key,
                    $"{holder.Lifetime} service '{holder.Key}' cannot depend on {node.Lifetime} service '{node.Key}'. " +
                    $"Path: {string.Join(" -> ", chain)}"));
            }

            var holds = IsRestrictive(node.Lifetime);
            path.Add(node);
            onPath.Add(node);
            if (holds) holders.Add(node);

            foreach (var edge in graph.NeedsOf(node).Where(e => !e.Deferred))
                Walk(edge.To);

            if (holds) holders.RemoveAt(holders.Count - 1);
            onPath.Remove(node);
            path.RemoveAt(path.Count - 1);
        }
    }
}
