using Maxen.DI.Interception;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Adds ten to whatever the call returns.</summary>
public sealed class AddTenInterceptor : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        invocation.Proceed();
        invocation.ReturnValue = (int)invocation.ReturnValue! + 10;
    }
}
