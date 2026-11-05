namespace Maxen.DI.Tests.Fixtures;

/// <summary>
/// A random set of services: each has a lifetime and depends only on services with a higher number, so the graph
/// has no loops. It also knows what a correct container must do with it, which is the reference the tests compare against.
/// </summary>
public sealed class GraphPlan
{
    public const int Transient = 0;
    public const int Scoped = 1;
    public const int Singleton = 2;

    private GraphPlan(int[] lifetimes, int[][] dependencies)
    {
        Lifetimes = lifetimes;
        Dependencies = dependencies;
    }

    public int[] Lifetimes { get; }

    public int[][] Dependencies { get; }

    public int Count => Lifetimes.Length;

    public static GraphPlan Random(int seed)
    {
        var random = new Random(seed);
        var count = random.Next(3, 12);

        var lifetimes = new int[count];
        var dependencies = new int[count][];
        for (var i = 0; i < count; i++)
        {
            lifetimes[i] = random.Next(3);
            var deps = new List<int>();
            for (var j = i + 1; j < count; j++)
                if (random.NextDouble() < 0.35) deps.Add(j);
            dependencies[i] = deps.ToArray();
        }
        return new GraphPlan(lifetimes, dependencies);
    }

    /// <summary>
    /// True if building <paramref name="node"/> must fail: somewhere below it a singleton, directly or through other
    /// services, ends up holding a scoped service.
    /// </summary>
    public bool IsCaptive(int node) => IsCaptive(node, new List<int>());

    private bool IsCaptive(int node, List<int> path)
    {
        if (Lifetimes[node] == Scoped && path.Any(ancestor => Lifetimes[ancestor] == Singleton)) return true;

        path.Add(node);
        var found = Dependencies[node].Any(dependency => IsCaptive(dependency, path));
        path.RemoveAt(path.Count - 1);
        return found;
    }

    public override string ToString()
        => string.Join("; ", Enumerable.Range(0, Count).Select(i =>
            $"{i}:{new[] { "T", "Sc", "Si" }[Lifetimes[i]]}->[{string.Join(",", Dependencies[i])}]"));
}
