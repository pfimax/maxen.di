using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Maxen.DI.Interception;

/// <summary>Walks one call through the interceptor chain and finally to the real method.</summary>
internal sealed class Invocation : IInvocation
{
    private readonly IReadOnlyList<IInterceptor> _interceptors;

    // Which interceptor is currently running. -1 means "none yet".
    private int _position = -1;

    // Generated proxies pass a direct call here, so reaching the real method needs no reflection.
    private readonly Func<object, object?[], object?>? _terminal;

    public Invocation(
        MethodInfo method, object?[] arguments, object target, IReadOnlyList<IInterceptor> interceptors,
        Func<object, object?[], object?>? terminal = null)
    {
        _terminal = terminal;
        Method = method;
        Arguments = arguments;
        Target = target;
        _interceptors = interceptors;
    }

    public MethodInfo Method { get; }
    public object?[] Arguments { get; }
    public object Target { get; }
    public object? ReturnValue { get; set; }

    public void Proceed()
    {
        // Remember where we were and restore it afterwards, so an interceptor
        // can call Proceed more than once (a retry, for example) and still get the same chain.
        var previous = _position;
        _position++;
        try
        {
            if (_position < _interceptors.Count)
                _interceptors[_position].Intercept(this);
            else
                ReturnValue = InvokeTarget();
        }
        finally
        {
            _position = previous;
        }
    }

    private object? InvokeTarget()
    {
        // A direct call throws the service's own exception, so there is no reflection wrapper to peel off.
        if (_terminal is not null) return _terminal(Target, Arguments);

        try
        {
            return Method.Invoke(Target, Arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Callers should see the exception the service threw, not the reflection wrapper.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable, keeps the compiler happy
        }
    }
}
