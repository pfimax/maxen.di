using Maxen.DI.Extensions.AspNetCore;

namespace Maxen.DI.Samples.WebApi;

/// <summary>Reads the current request through <see cref="RequestValues"/>, with no HttpContext plumbing.</summary>
public sealed class WhoAmI
{
    private readonly RequestValues _request;

    public WhoAmI(RequestValues request) => _request = request;

    public string Describe() => $"{_request.Request.Method} {_request.Request.Path} (trace {_request.TraceIdentifier})";
}
