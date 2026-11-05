using Maxen.DI.Interception;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Doubles whatever the call returns.</summary>
public sealed class TimesTwoInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        invocation.Proceed();
        invocation.ReturnValue = (int)invocation.ReturnValue! * 2;
    }
}
