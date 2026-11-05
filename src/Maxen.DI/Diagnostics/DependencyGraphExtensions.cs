using System.Text;

namespace Maxen.DI.Diagnostics;

/// <summary>Turns a <see cref="DependencyGraph"/> into text you can paste into documentation or a diagram tool.</summary>
public static class DependencyGraphExtensions
{
    /// <summary>A Mermaid flowchart. Dotted arrows are deferred dependencies (Lazy and Func).</summary>
    public static string ToMermaid(this DependencyGraph graph)
    {
        var ids = NumberNodes(graph);
        var text = new StringBuilder("graph LR").AppendLine();

        foreach (var node in graph.Nodes)
            text.Append("    ").Append(ids[node]).Append("[\"").Append(Label(node).Replace("\"", "'")).AppendLine("\"]");

        foreach (var edge in graph.Edges)
            text.Append("    ").Append(ids[edge.From]).Append(edge.Deferred ? " -.-> " : " --> ").AppendLine(ids[edge.To]);

        return text.ToString();
    }

    /// <summary>A Graphviz DOT digraph. Dashed edges are deferred dependencies.</summary>
    public static string ToDot(this DependencyGraph graph)
    {
        var ids = NumberNodes(graph);
        var text = new StringBuilder("digraph Dependencies {").AppendLine();
        text.AppendLine("    rankdir=LR;");

        foreach (var node in graph.Nodes)
            text.Append("    ").Append(ids[node]).Append(" [label=\"").Append(Label(node).Replace("\"", "'")).AppendLine("\"];");

        foreach (var edge in graph.Edges)
            text.Append("    ").Append(ids[edge.From]).Append(" -> ").Append(ids[edge.To])
                .AppendLine(edge.Deferred ? " [style=dashed];" : ";");

        return text.Append('}').AppendLine().ToString();
    }

    /// <summary>A plain list, one registration per line with what it needs.</summary>
    public static string ToText(this DependencyGraph graph)
    {
        var text = new StringBuilder();
        foreach (var node in graph.Nodes)
        {
            text.AppendLine(node.ToString());
            foreach (var edge in graph.EdgesFrom(node))
                text.Append("    needs ").Append(edge.To.Key).AppendLine(edge.Deferred ? " (deferred)" : "");
        }
        return text.ToString();
    }

    private static Dictionary<ServiceDescriptor, string> NumberNodes(DependencyGraph graph)
    {
        // Type names contain characters diagram tools choke on, so nodes get plain ids and the real name goes in the label.
        var ids = new Dictionary<ServiceDescriptor, string>();
        foreach (var node in graph.Nodes)
            ids[node] = "n" + ids.Count;
        return ids;
    }

    private static string Label(ServiceDescriptor node) => $"{node.Key} ({node.Lifetime})";
}
