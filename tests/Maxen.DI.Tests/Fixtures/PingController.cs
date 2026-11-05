using Microsoft.AspNetCore.Mvc;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>An MVC controller whose constructor needs a scoped service, to prove controllers are built by the container.</summary>
[ApiController]
[Route("ping")]
public sealed class PingController : ControllerBase
{
    private readonly RequestId _request;

    public PingController(RequestId request) => _request = request;

    [HttpGet]
    public string Get() => _request.Value.ToString();
}
