using System.Reflection;

namespace Maxen.DI.Interception;

/// <summary>
/// The base class of the proxies the source generator writes. A generated proxy implements a service interface
/// and sends each call through <see cref="Call"/>, which runs the interceptors and finally the real method, without
/// reflection or runtime code generation. You never derive from this yourself.
/// </summary>
public abstract class GeneratedProxyBase
{
    private readonly object _target;
    private readonly IReadOnlyList<IInterceptor> _interceptors;

    /// <summary>Creates a proxy around <paramref name="target"/> that runs <paramref name="interceptors"/> on every call.</summary>
    protected GeneratedProxyBase(object target, IReadOnlyList<IInterceptor> interceptors)
    {
        _target = target;
        _interceptors = interceptors;
    }

    /// <summary>
    /// Runs one call through the interceptors. <paramref name="terminal"/> is the direct call to the real object,
    /// used when the last interceptor proceeds.
    /// </summary>
    protected object? Call(MethodInfo method, object?[] arguments, Func<object, object?[], object?> terminal)
    {
        var invocation = new Invocation(method, arguments, _target, _interceptors, terminal);
        invocation.Proceed();
        return invocation.ReturnValue;
    }
}
