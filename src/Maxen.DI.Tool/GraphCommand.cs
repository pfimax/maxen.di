using Maxen.DI.Diagnostics;

namespace Maxen.DI.Tool;

/// <summary>Writes an assembly's dependency graph, and (for <c>validate</c>, or <c>--validate</c>) checks it.</summary>
internal static class GraphCommand
{
    public static int Run(ToolArguments arguments, TextWriter output, TextWriter error)
        => ModuleInspector.Inspect(arguments.AssemblyPath, arguments.Profiles, error, container =>
        {
            // "validate" is the quiet form: CI only wants the verdict.
            if (arguments.Command == "graph")
            {
                var graph = container.BuildGraph();
                output.WriteLine(arguments.Format switch
                {
                    "dot" => graph.ToDot(),
                    "text" => graph.ToText(),
                    "json" => GraphSnapshot.Of(graph).ToJson(),
                    _ => graph.ToMermaid(),
                });
            }

            if (!arguments.Validate) return 0;

            var issues = container.Validate();
            foreach (var issue in issues) error.WriteLine(issue);
            return issues.Count == 0 ? 0 : 1;
        });
}
