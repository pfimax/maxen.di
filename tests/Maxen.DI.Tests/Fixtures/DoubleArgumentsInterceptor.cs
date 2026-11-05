using Maxen.DI.Interception;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Doubles every int argument before letting the call through, to show interceptors can rewrite arguments.</summary>
public sealed class DoubleArgumentsInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        for (var i = 0; i < invocation.Arguments.Length; i++)
            if (invocation.Arguments[i] is int number) invocation.Arguments[i] = number * 2;

        invocation.Proceed();
    }
}
