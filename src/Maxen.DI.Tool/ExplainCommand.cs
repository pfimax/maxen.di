using Maxen.DI.Diagnostics;

namespace Maxen.DI.Tool;

/// <summary>Answers "what happens when someone asks for this service?": which registration wins, and what it needs and who needs it.</summary>
internal static class ExplainCommand
{
    public static int Run(ToolArguments arguments, TextWriter output, TextWriter error)
        => ModuleInspector.Inspect(arguments.AssemblyPath, arguments.Profiles, error, container =>
        {
            var graph = container.BuildGraph();
            var wanted = arguments.ServiceName;

            // Match the full name or just the simple one, so "IClock" works without its namespace.
            var keys = graph.Nodes
                .Select(n => n.Key)
                .Where(k => string.Equals(k.Type.FullName, wanted, StringComparison.Ordinal)
                    || string.Equals(k.Type.Name, wanted, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            if (keys.Count == 0)
            {
                error.WriteLine($"No registration found for '{wanted}'.");
                return 1;
            }

            foreach (var key in keys)
                Describe(graph, key, output);
            return 0;
        });

    private static void Describe(DependencyGraph graph, ServiceKey key, TextWriter output)
    {
        output.WriteLine(key);

        // Nodes keep registration order within a key, so the last one is the winner.
        var all = graph.Nodes.Where(n => n.Key == key).ToList();
        for (var i = 0; i < all.Count; i++)
        {
            var descriptor = all[i];
            var notes = new List<string>();
            if (i == all.Count - 1) notes.Add("wins unless a condition rejects it");
            if (descriptor.Condition is not null) notes.Add("conditional");
            output.WriteLine($"  {i + 1}. {descriptor}{(notes.Count > 0 ? "  [" + string.Join(", ", notes) + "]" : string.Empty)}");
        }

        // What the winning registration pulls in, and who pulls it in.
        var winner = all[^1];
        var needs = graph.EdgesFrom(winner).Select(e => $"{e.To.Key}{(e.Deferred ? " (deferred)" : string.Empty)}").ToList();
        var neededBy = graph.Edges.Where(e => all.Contains(e.To)).Select(e => e.From.Key.ToString()).Distinct().ToList();

        output.WriteLine($"  needs:     {(needs.Count == 0 ? "nothing" : string.Join(", ", needs))}");
        output.WriteLine($"  needed by: {(neededBy.Count == 0 ? "nothing" : string.Join(", ", neededBy))}");
    }
}
