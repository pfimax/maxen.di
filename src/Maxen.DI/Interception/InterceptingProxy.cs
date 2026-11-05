using System.Reflection;

namespace Maxen.DI.Interception;

/// <summary>
/// The runtime proxy that implements a service interface and routes each call through the
/// interceptors. It has to be public and non-sealed because <see cref="DispatchProxy"/> derives from it.
/// You never create it yourself; use <c>InterceptWith</c> when registering.
/// </summary>
public class InterceptingProxy : DispatchProxy
{
    private object? _target;
    private IReadOnlyList<IInterceptor>? _interceptors;

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Interface proxies are created at run time.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Interface proxies are created at run time.")]
    internal static object Create(Type serviceInterface, object target, IReadOnlyList<IInterceptor> interceptors)
    {
        var proxy = Create(serviceInterface, typeof(InterceptingProxy));
        var typed = (InterceptingProxy)proxy;
        typed._target = target;
        typed._interceptors = interceptors;
        return proxy;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var invocation = new Invocation(targetMethod!, args ?? Array.Empty<object?>(), _target!, _interceptors!);
        invocation.Proceed();
        return invocation.ReturnValue;
    }
}
