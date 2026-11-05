using Maxen.DI.Interception;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>An interceptor that changes the result of string-returning calls.</summary>
public sealed class ShoutInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        invocation.Proceed();
        if (invocation.ReturnValue is string text)
            invocation.ReturnValue = text + "!";
    }
}
