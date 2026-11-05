using Maxen.DI.Pipeline;

namespace Maxen.DI.Interception;

/// <summary>Registration helpers for interceptors.</summary>
public static class InterceptionExtensions
{
    /// <summary>Intercepts this registration with a ready-made interceptor. The service must be an interface.</summary>
    public static RegistrationBuilder InterceptWith(this RegistrationBuilder registration, IInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        return registration.Use(MiddlewarePhase.PostLifetime, new InterceptionMiddleware(_ => interceptor, _ => true));
    }

    /// <summary>Intercepts this registration with an interceptor taken from the container (so it can have dependencies).</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Interface proxies are created at run time.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Interface proxies are created at run time.")]
    public static RegistrationBuilder InterceptWith<TInterceptor>(this RegistrationBuilder registration)
        where TInterceptor : class, IInterceptor
        => registration.Use(
            MiddlewarePhase.PostLifetime,
            new InterceptionMiddleware(ctx => ctx.Resolve<TInterceptor>(), _ => true));

    /// <summary>
    /// Intercepts every registration of <typeparamref name="TService"/> without touching each one.
    /// Calls to the interceptors happen in the order the interceptors were added.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Interface proxies are created at run time.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Interface proxies are created at run time.")]
    public static ContainerBuilder Intercept<TService, TInterceptor>(this ContainerBuilder builder)
        where TService : class
        where TInterceptor : class, IInterceptor
    {
        if (!typeof(TService).IsInterface)
            throw new RegistrationException($"'{typeof(TService).Name}' is not an interface, so it cannot be intercepted.");

        return builder.Use(
            MiddlewarePhase.PostLifetime,
            new InterceptionMiddleware(ctx => ctx.Resolve<TInterceptor>(), ctx => ctx.Key.Type == typeof(TService)));
    }
}
