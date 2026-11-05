using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Maxen.DI.Extensions.AspNetCore;

/// <summary>An endpoint that shows what the container holds and whether it would hold up, for dashboards and health probes.</summary>
public static class DiagnosticsEndpointExtensions
{
    /// <summary>
    /// Maps a GET endpoint that answers with the registrations, the dependencies between them and any validation
    /// problems, as JSON. It answers 200 when the container is sound and 503 when validation found problems, so it
    /// can double as a readiness probe. The picture includes type names, so keep it behind authorization in production:
    /// the returned builder lets you call <c>RequireAuthorization()</c> on it.
    /// </summary>
    /// <param name="endpoints">Where to add the route.</param>
    /// <param name="pattern">The route, "/_maxen" by default.</param>
    public static RouteHandlerBuilder MapMaxenDiagnostics(this IEndpointRouteBuilder endpoints, string pattern = "/_maxen")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet(pattern, (HttpContext context) =>
        {
            // The request scope knows its root container; a plain Microsoft provider has nothing to show.
            if (context.GetMaxenScope()?.Root is not Container container)
                return Results.Problem("This app is not running on Maxen.DI.", statusCode: StatusCodes.Status501NotImplemented);

            var graph = container.BuildGraph();
            var issues = container.Validate();

            var body = new
            {
                healthy = issues.Count == 0,
                registrations = graph.Nodes.Select(n => new
                {
                    service = n.Key.ToString(),
                    lifetime = n.Lifetime.ToString(),
                    implementation = n.ToString(),
                }),
                dependencies = graph.Edges.Select(e => new
                {
                    from = e.From.Key.ToString(),
                    to = e.To.Key.ToString(),
                    deferred = e.Deferred,
                }),
                issues = issues.Select(i => new { kind = i.Kind.ToString(), service = i.Service.ToString(), message = i.Message }),
            };

            return Results.Json(body, statusCode: issues.Count == 0 ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });
    }
}
