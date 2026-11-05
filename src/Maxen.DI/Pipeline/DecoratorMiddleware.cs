using Maxen.DI.Internal;

namespace Maxen.DI.Pipeline;

/// <summary>Builds the real service, then wraps it in a decorator that receives it as its inner service.</summary>
internal sealed class DecoratorMiddleware : IResolutionMiddleware
{
    private readonly Type _serviceType;
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly Type _decoratorType;
    private readonly InstanceFactory _factory;

    public DecoratorMiddleware(Type serviceType, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType, InstanceFactory factory)
    {
        _serviceType = serviceType;
        _decoratorType = decoratorType;
        _factory = factory;
    }

    public object Invoke(ResolveContext context, ResolveDelegate next)
    {
        var inner = next(context);

        // The lifetime only sees the outermost decorator, so the wrapped instance would
        // never be disposed unless we register it here.
        if (context.Descriptor.OwnsInstance)
            context.Scope.Instances.Track(inner);

        // The decorator's constructor asks for the service it decorates; hand it the inner
        // instance directly instead of resolving (which would loop back to ourselves).
        return _factory.Create(_decoratorType, context, new Dictionary<Type, object> { [_serviceType] = inner });
    }
}
