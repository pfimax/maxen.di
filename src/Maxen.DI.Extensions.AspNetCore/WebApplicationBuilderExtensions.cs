using Maxen.DI.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace Maxen.DI.Extensions.AspNetCore;

/// <summary>One-line setup for <c>WebApplication.CreateBuilder()</c>.</summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Makes the web app use Maxen.DI as its container. Everything added through <c>builder.Services</c>
    /// is carried over; the optional callback is for Maxen-only features such as decorators or interceptors.
    /// Every request still gets its own scope, because ASP.NET Core asks the container for one.
    /// </summary>
    public static WebApplicationBuilder UseMaxenDI(this WebApplicationBuilder builder, Action<ContainerBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Host.UseMaxenDI(configure);
        return builder;
    }
}
