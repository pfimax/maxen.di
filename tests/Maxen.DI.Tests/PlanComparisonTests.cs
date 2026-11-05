using System.Text;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// Every combination of lifetimes for a six-class graph (3^6 = 729), resolved in several ways, with resolve plans on and
/// off. The shape of each result (which instances are shared), every error message and the disposal counts must be
/// identical. This is the proof that plans are only a shortcut.
/// </summary>
public class PlanComparisonTests
{
    private static readonly Type[] Nodes =
    {
        typeof(ChainNode0), typeof(ChainNode1), typeof(ChainNode2), typeof(ChainNode3), typeof(ChainNode4), typeof(ChainNode5),
    };

    private static readonly Lifetime[] Lifetimes = { Lifetime.Transient, Lifetime.Scoped, Lifetime.Singleton };

    // All 729 take about a minute per framework (every container compiles its constructors again), so a normal run
    // checks every ninth combination. CI sets MAXEN_EXHAUSTIVE=1 to check them all.
    public static IEnumerable<object[]> Combinations()
    {
        var step = Environment.GetEnvironmentVariable("MAXEN_EXHAUSTIVE") == "1" ? 1 : 9;
        for (var code = 0; code < 729; code += step) yield return new object[] { code };
    }

    private static Lifetime[] Decode(int code)
    {
        var result = new Lifetime[Nodes.Length];
        for (var i = 0; i < result.Length; i++, code /= 3) result[i] = Lifetimes[code % 3];
        return result;
    }

    // Turns a resolved tree into text where every distinct object gets a number, so sharing shows up as repeated numbers.
    private static string Describe(ChainNode node, Dictionary<ChainNode, int> ids)
    {
        if (ids.TryGetValue(node, out var known)) return "#" + known;

        var id = ids.Count;
        ids[node] = id;
        var children = string.Join(",", node.Children.Select(c => Describe(c, ids)));
        return $"{node.GetType().Name}#{id}[{children}]";
    }

    private static string Run(int code, bool plans)
    {
        var lifetimes = Decode(code);
        var builder = new ContainerBuilder();
        builder.Options.UseResolvePlans = plans;
        for (var i = 0; i < Nodes.Length; i++)
            builder.Register(Nodes[i], Nodes[i]).WithLifetime(lifetimes[i]);

        var container = builder.Build();
        var scopes = new[] { container.CreateScope(), container.CreateScope() };
        var ids = new Dictionary<ChainNode, int>(ReferenceEqualityComparer.Instance);
        var log = new StringBuilder();

        for (var node = 0; node < Nodes.Length; node++)
        {
            // Twice in scope 0, once in scope 1, once straight from the root.
            foreach (var where in new[] { 0, 0, 1, -1 })
            {
                IScope scope = where < 0 ? container : scopes[where];
                try
                {
                    var resolved = (ChainNode)scope.Resolve(new ServiceKey(Nodes[node]));
                    log.Append($"[{where}] {Describe(resolved, ids)}\n");
                }
                catch (Exception ex)
                {
                    log.Append($"[{where}] {ex.GetType().Name}: {ex.Message}\n");
                }
            }
        }

        scopes[0].Dispose();
        scopes[1].Dispose();
        container.Dispose();

        // Anything built must be disposed exactly once, in both modes.
        foreach (var pair in ids.OrderBy(p => p.Value))
            log.Append($"dispose #{pair.Value}: {pair.Key.DisposeCount}\n");

        return log.ToString();
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Plans_and_the_pipeline_give_identical_results(int code)
    {
        var withPlans = Run(code, plans: true);
        var without = Run(code, plans: false);

        Assert.True(withPlans == without,
            $"Lifetimes {string.Join(",", Decode(code).Select(l => l.Name))} differ.\n--- plans ---\n{withPlans}\n--- pipeline ---\n{without}");
    }
}
