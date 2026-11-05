using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// Builds hundreds of random registration graphs and checks the container against a plain reference model:
/// which resolves must fail as captive dependencies, how instances are shared, and that everything built is disposed once.
/// </summary>
public class RandomGraphTests
{
    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 300).Select(seed => new object[] { seed });

    private static Container Build(GraphPlan plan, GraphCounters counters)
    {
        var builder = new ContainerBuilder();
        for (var i = 0; i < plan.Count; i++)
        {
            var id = i;
            var registration = builder.RegisterFactory<GraphNode>(ctx =>
            {
                var children = plan.Dependencies[id]
                    .Select(d => (GraphNode)ctx.Resolve(new ServiceKey(typeof(GraphNode), d)))
                    .ToArray();
                return new GraphNode(id, children, counters);
            }).WithKey(id);

            switch (plan.Lifetimes[i])
            {
                case GraphPlan.Scoped: registration.AsScoped(); break;
                case GraphPlan.Singleton: registration.AsSingleton(); break;
                default: registration.AsTransient(); break;
            }
        }
        return builder.Build();
    }

    // Every distinct instance reachable from one resolved root, plus how many different instances point at each.
    // A shared parent is walked only once, so its children are not counted again for every path that reaches it.
    private static (List<GraphNode> Instances, Dictionary<GraphNode, int> Owners) Walk(GraphNode root)
    {
        var instances = new List<GraphNode>();
        var owners = new Dictionary<GraphNode, int>(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<GraphNode>(ReferenceEqualityComparer.Instance);

        void Visit(GraphNode node)
        {
            if (!seen.Add(node)) return;
            instances.Add(node);
            foreach (var child in node.Children)
            {
                owners[child] = owners.GetValueOrDefault(child) + 1;
                Visit(child);
            }
        }

        Visit(root);
        return (instances, owners);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void The_container_agrees_with_the_reference_model(int seed)
    {
        var plan = GraphPlan.Random(seed);
        var counters = new GraphCounters();
        var container = Build(plan, counters);
        var scopes = new[] { container.CreateScope(), container.CreateScope() };
        var random = new Random(seed * 7919);

        var singletons = new Dictionary<int, GraphNode>();
        var scoped = new Dictionary<(int Scope, int Node), GraphNode>();

        // Resolve every node a few times from both scopes, in a random order.
        var requests = Enumerable.Range(0, plan.Count).SelectMany(n => new[] { (n, 0), (n, 1), (n, 0) })
            .OrderBy(_ => random.Next()).ToList();

        foreach (var (node, scopeIndex) in requests)
        {
            var expectCaptive = plan.IsCaptive(node);
            GraphNode? resolved = null;
            Exception? failure = null;
            try { resolved = (GraphNode)scopes[scopeIndex].Resolve(new ServiceKey(typeof(GraphNode), node)); }
            catch (ScopeValidationException ex) { failure = ex; }

            Assert.True(expectCaptive == (failure is not null),
                $"seed {seed}, node {node}: expected captive={expectCaptive}, got {(failure is null ? "success" : "ScopeValidationException")}. Plan: {plan}");
            if (resolved is null) continue;

            var (tree, owners) = Walk(resolved);
            foreach (var instance in tree)
            {
                switch (plan.Lifetimes[instance.Id])
                {
                    case GraphPlan.Singleton:
                        // One instance for the whole container, whichever scope asked.
                        if (singletons.TryGetValue(instance.Id, out var known))
                            Assert.True(ReferenceEquals(known, instance), $"seed {seed}: singleton {instance.Id} was built twice. Plan: {plan}");
                        else singletons[instance.Id] = instance;
                        break;

                    case GraphPlan.Scoped:
                        // One per scope: same inside a scope, different between scopes.
                        var key = (scopeIndex, instance.Id);
                        if (scoped.TryGetValue(key, out var inScope))
                            Assert.True(ReferenceEquals(inScope, instance), $"seed {seed}: scoped {instance.Id} was built twice in one scope. Plan: {plan}");
                        else scoped[key] = instance;
                        break;
                }
            }

            // A transient is a new object for every owner that asks for it, so no transient has two owners.
            var sharedTransient = tree.FirstOrDefault(i => plan.Lifetimes[i.Id] == GraphPlan.Transient && owners.GetValueOrDefault(i) > 1);
            Assert.True(sharedTransient is null,
                $"seed {seed}: transient {sharedTransient?.Id} was shared between owners. Plan: {plan}");
        }

        foreach (var key in scoped.Keys.Where(k => scoped.Keys.Any(o => o.Node == k.Node && o.Scope != k.Scope)))
            Assert.NotSame(scoped[(0, key.Node)], scoped[(1, key.Node)]);

        // Tear everything down: whatever was built must be disposed, and only once.
        scopes[0].Dispose();
        scopes[1].Dispose();
        container.Dispose();

        Assert.True(counters.CreatedCount == counters.DisposedCount,
            $"seed {seed}: {counters.CreatedCount} created but {counters.DisposedCount} disposed. Plan: {plan}");
        Assert.Equal(0, counters.DisposedTwiceCount);
    }
}
