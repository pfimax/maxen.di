namespace Maxen.DI.Tool;

/// <summary>Compares the graphs of two builds, so a pull request can show which registrations or dependencies it changed.</summary>
internal static class DiffCommand
{
    public static int Run(ToolArguments arguments, TextWriter output, TextWriter error)
    {
        GraphSnapshot? before = null;
        GraphSnapshot? after = null;
        int code;

        if (arguments.BaselinePath.Length > 0)
        {
            // Compare the build against a saved snapshot: the snapshot is "before", the assembly is "after".
            if (!File.Exists(arguments.BaselinePath))
            {
                error.WriteLine($"Snapshot not found: {Path.GetFullPath(arguments.BaselinePath)}");
                return 2;
            }

            try { before = GraphSnapshot.FromJson(File.ReadAllText(arguments.BaselinePath)); }
            catch (System.Text.Json.JsonException ex)
            {
                error.WriteLine($"'{arguments.BaselinePath}' is not a snapshot written by 'maxen-di snapshot': {ex.Message}");
                return 2;
            }

            code = ModuleInspector.Inspect(arguments.AssemblyPath, arguments.Profiles, error,
                c => { after = GraphSnapshot.Of(c.BuildGraph()); return 0; });
            if (code != 0) return code;
        }
        else
        {
            // Each assembly is loaded and unloaded on its own; only the text snapshot survives.
            code = ModuleInspector.Inspect(arguments.AssemblyPath, arguments.Profiles, error,
                c => { before = GraphSnapshot.Of(c.BuildGraph()); return 0; });
            if (code != 0) return code;

            code = ModuleInspector.Inspect(arguments.OtherAssemblyPath, arguments.Profiles, error,
                c => { after = GraphSnapshot.Of(c.BuildGraph()); return 0; });
            if (code != 0) return code;
        }

        var changes = 0;
        changes += Print(output, "- registration", before!.Nodes.Except(after!.Nodes));
        changes += Print(output, "+ registration", after.Nodes.Except(before.Nodes));
        changes += Print(output, "- dependency", before.Edges.Except(after.Edges));
        changes += Print(output, "+ dependency", after.Edges.Except(before.Edges));

        if (changes == 0) output.WriteLine("No differences.");
        return changes == 0 ? 0 : 1;
    }

    private static int Print(TextWriter output, string label, IEnumerable<string> lines)
    {
        var count = 0;
        foreach (var line in lines.OrderBy(l => l, StringComparer.Ordinal))
        {
            output.WriteLine($"{label}: {line}");
            count++;
        }
        return count;
    }
}
