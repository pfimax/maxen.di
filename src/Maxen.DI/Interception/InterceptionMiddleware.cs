using Maxen.DI.Pipeline;

namespace Maxen.DI.Interception;

/// <summary>Wraps a freshly built service in a proxy that runs the given interceptors.</summary>
internal sealed class InterceptionMiddleware : IResolutionMiddleware
{
    private readonly Func<ResolveContext, IInterceptor> _interceptorFactory;
    private readonly Func<ResolveContext, bool> _appliesTo;

    public InterceptionMiddleware(Func<ResolveContext, IInterceptor> interceptorFactory, Func<ResolveContext, bool> appliesTo)
    {
        _interceptorFactory = interceptorFactory;
        _appliesTo = appliesTo;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only reachable through InterceptWith/Intercept, which are marked RequiresUnreferencedCode.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only reachable through InterceptWith/Intercept, which are marked RequiresDynamicCode.")]
    public object Invoke(ResolveContext context, ResolveDelegate next)
    {
        var instance = next(context);
        if (!_appliesTo(context)) return instance;

        var serviceType = context.Key.Type;
        if (!serviceType.IsInterface)
            throw new ResolutionException(
                $"Cannot intercept '{context.Key}': only interface services can be proxied.");

        // The lifetime will only see the proxy, so make sure the real object is still disposed.
        if (context.Descriptor.OwnsInstance)
            context.Scope.Instances.Track(instance);

        // Interceptors can be services themselves (with their own dependencies), hence the factory.
        var interceptors = new[] { _interceptorFactory(context) };

        // A proxy the generator wrote needs no runtime code generation, so it is always preferred.
        if (GeneratedProxies.TryGet(serviceType, out var generated))
            return generated(instance, interceptors);

        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
            throw new ResolutionException(
                $"No generated proxy exists for '{context.Key}', and this runtime cannot create one. " +
                "Intercept it with InterceptWith / Intercept in code the source generator can see, and make sure the interface only uses plain methods and properties.");

        return InterceptingProxy.Create(serviceType, instance, interceptors);
    }
}
