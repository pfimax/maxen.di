using Maxen.DI.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Maxen.DI.Tool;

/// <summary>
/// A graph reduced to plain text lines (one per registration, one per dependency), so two graphs can be compared
/// after the assemblies they came from are unloaded.
/// </summary>
internal sealed class GraphSnapshot
{
    private GraphSnapshot(HashSet<string> nodes, HashSet<string> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>One line per registration: the service, what builds it and its lifetime.</summary>
    public IReadOnlySet<string> Nodes { get; }

    /// <summary>One line per dependency: "A -> B", marked when B is only resolved later (Lazy, Func).</summary>
    public IReadOnlySet<string> Edges { get; }

    public static GraphSnapshot Of(DependencyGraph graph)
    {
        var nodes = graph.Nodes.Select(n => n.ToString()).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges
            .Select(e => $"{e.From.Key} -> {e.To.Key}{(e.Deferred ? " (deferred)" : string.Empty)}")
            .ToHashSet(StringComparer.Ordinal);
        return new GraphSnapshot(nodes, edges);
    }

    /// <summary>The snapshot as indented JSON with sorted entries, so two runs of the same build give identical files.</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            WriteArray(writer, "nodes", Nodes);
            WriteArray(writer, "edges", Edges);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reads a snapshot written by <see cref="ToJson"/>.</summary>
    public static GraphSnapshot FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new GraphSnapshot(ReadArray(document.RootElement, "nodes"), ReadArray(document.RootElement, "edges"));
    }

    private static void WriteArray(Utf8JsonWriter writer, string name, IEnumerable<string> lines)
    {
        writer.WriteStartArray(name);
        foreach (var line in lines.OrderBy(l => l, StringComparer.Ordinal)) writer.WriteStringValue(line);
        writer.WriteEndArray();
    }

    private static HashSet<string> ReadArray(JsonElement root, string name)
        => root.TryGetProperty(name, out var array)
            ? array.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
}
