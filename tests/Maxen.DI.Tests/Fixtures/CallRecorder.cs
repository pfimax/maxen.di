using Maxen.DI.Interception;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>An interceptor that records the name of every method called through it.</summary>
public sealed class CallRecorder : IInterceptor
{
    public List<string> Calls { get; } = new();

    public void Intercept(IInvocation invocation)
    {
        Calls.Add(invocation.Method.Name);
        invocation.Proceed();
    }
}
