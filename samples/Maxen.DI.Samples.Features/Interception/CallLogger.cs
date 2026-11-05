using Maxen.DI.Interception;

namespace Maxen.DI.Samples.Features.Interception;

/// <summary>Sits in front of every method call on the service and can look at, change or skip the call.</summary>
internal sealed class CallLogger : IInterceptor
{
    public void Intercept(IInvocation invocation)
    {
        Console.WriteLine($"  -> {invocation.Method.Name}({string.Join(", ", invocation.Arguments)})");
        invocation.Proceed();
        Console.WriteLine($"  <- {invocation.ReturnValue}");
    }
}
