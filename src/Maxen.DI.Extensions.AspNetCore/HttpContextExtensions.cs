using Maxen.DI.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace Maxen.DI.Extensions.AspNetCore;

/// <summary>Reaching Maxen-specific features from inside a request.</summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// The Maxen scope that belongs to this request, or null if the app isn't running on Maxen.DI.
    /// Use it for things the standard provider can't do, such as resolving with arguments.
    /// </summary>
    public static IScope? GetMaxenScope(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (context.RequestServices as MaxenServiceProvider)?.Scope;
    }
}
