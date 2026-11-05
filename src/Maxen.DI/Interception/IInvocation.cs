using System.Reflection;

namespace Maxen.DI.Interception;

/// <summary>A single call to a method on an intercepted service.</summary>
public interface IInvocation
{
    /// <summary>The interface method that was called.</summary>
    MethodInfo Method { get; }

    /// <summary>The arguments of the call. You may change them before calling <see cref="Proceed"/>.</summary>
    object?[] Arguments { get; }

    /// <summary>The real object behind the proxy.</summary>
    object Target { get; }

    /// <summary>The result of the call. Set after <see cref="Proceed"/>, or set it yourself to short-circuit.</summary>
    object? ReturnValue { get; set; }

    /// <summary>Continues to the next interceptor, or to the real method if this is the last one.</summary>
    void Proceed();
}
