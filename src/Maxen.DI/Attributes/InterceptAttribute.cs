namespace Maxen.DI;

/// <summary>
/// Puts an interceptor in front of a <see cref="ServiceAttribute"/> class, like <c>InterceptWith(new T())</c> would.
/// The service type must be an interface, and the interceptor needs a public parameterless constructor. With the source
/// generator the proxy is written at compile time, so this works with native AOT; <c>RegisterAttributed</c> honours it too.
/// Use it several times to stack interceptors; <see cref="Order"/> says which goes first.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class InterceptAttribute : Attribute
{
    /// <summary>Intercepts the class's service with <paramref name="interceptorType"/>, an <c>IInterceptor</c>.</summary>
    public InterceptAttribute(Type interceptorType) => InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType));

    /// <summary>The interceptor class.</summary>
    public Type InterceptorType { get; }

    /// <summary>Interceptors with lower numbers run first (outermost). Equal numbers keep the order they are written in.</summary>
    public int Order { get; set; }
}
