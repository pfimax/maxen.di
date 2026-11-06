using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;

namespace Maxen.DI.Extensions.AspNetCore;

/// <summary>
/// What the diagnostics endpoint answers for one container, worked out on the first request and kept.
/// A container can't change after it is built, so the answer can't either, and recomputing it per request would
/// rerun every factory each time a probe calls when <c>ValidateFactories</c> is on.
/// </summary>
internal sealed class DiagnosticsReport
{
    // Keyed weakly, so a container that is disposed and collected doesn't stay alive because of its report.
    // The Lazy makes sure validation runs once even when the first requests arrive together.
    private static readonly ConditionalWeakTable<Container, Lazy<DiagnosticsReport>> Reports = new();

    private DiagnosticsReport(object body, int statusCode)
    {
        Body = body;
        StatusCode = statusCode;
    }

    public object Body { get; }

    public int StatusCode { get; }

    public static DiagnosticsReport For(Container container)
        => Reports.GetValue(container, c => new Lazy<DiagnosticsReport>(() => Build(c))).Value;

    private static DiagnosticsReport Build(Container container)
    {
        var graph = container.BuildGraph();
        var issues = container.Validate();

        // Materialized now: the report outlives this call, and the lists must not be re-read later.
        var body = new
        {
            healthy = issues.Count == 0,
            registrations = graph.Nodes.Select(n => new
            {
                service = n.Key.ToString(),
                lifetime = n.Lifetime.ToString(),
                implementation = n.ToString(),
            }).ToList(),
            dependencies = graph.Edges.Select(e => new
            {
                from = e.From.Key.ToString(),
                to = e.To.Key.ToString(),
                deferred = e.Deferred,
            }).ToList(),
            issues = issues.Select(i => new { kind = i.Kind.ToString(), service = i.Service.ToString(), message = i.Message }).ToList(),
        };

        return new DiagnosticsReport(body, issues.Count == 0 ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }
}
