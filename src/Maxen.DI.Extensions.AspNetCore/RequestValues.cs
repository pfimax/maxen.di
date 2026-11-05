using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Maxen.DI.Extensions.AspNetCore;

/// <summary>
/// The current request, as a scoped service. Take it in a constructor like any other dependency: the container builds
/// it normally (no factory involved) and the middleware from <c>UseMaxenRequestValues</c> fills it in at the start
/// of each request, so everything built later in that request sees the same values.
/// </summary>
public sealed class RequestValues
{
    private HttpContext? _context;

    /// <summary>The whole HTTP context of this request.</summary>
    public HttpContext HttpContext => _context ?? throw NotAttached();

    /// <summary>The incoming request.</summary>
    public HttpRequest Request => HttpContext.Request;

    /// <summary>The response being written.</summary>
    public HttpResponse Response => HttpContext.Response;

    /// <summary>The signed-in user, or an anonymous principal.</summary>
    public ClaimsPrincipal User => HttpContext.User;

    /// <summary>The id ASP.NET Core gave this request, handy for log correlation.</summary>
    public string TraceIdentifier => HttpContext.TraceIdentifier;

    /// <summary>True once the middleware has filled this in, so code that may also run outside a request can check first.</summary>
    public bool IsAttached => _context is not null;

    // Only the middleware in this assembly should set this.
    internal void Attach(HttpContext context) => _context = context;

    private static InvalidOperationException NotAttached()
        => new("RequestValues is empty. Call app.UseMaxenRequestValues() early in the pipeline, and only use it during a request.");
}
