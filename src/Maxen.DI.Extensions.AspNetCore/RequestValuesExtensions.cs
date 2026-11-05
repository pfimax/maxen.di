using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.AspNetCore;

/// <summary>Switching on <see cref="RequestValues"/>.</summary>
public static class RequestValuesExtensions
{
    /// <summary>Registers <see cref="RequestValues"/> as a scoped service. Call it before <c>Build()</c>.</summary>
    public static WebApplicationBuilder AddMaxenRequestValues(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // A plain type registration: the container constructs it itself, which is the point.
        builder.Services.AddScoped<RequestValues>();
        return builder;
    }

    /// <summary>Fills <see cref="RequestValues"/> at the start of every request. Put it before anything that needs the values.</summary>
    public static IApplicationBuilder UseMaxenRequestValues(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            context.RequestServices.GetRequiredService<RequestValues>().Attach(context);
            await next();
        });
    }
}
