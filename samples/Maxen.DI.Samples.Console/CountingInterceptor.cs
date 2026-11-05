using Maxen.DI.Interception;

namespace Maxen.DI.Samples.Console;

/// <summary>An interceptor that counts calls. Needs a public parameterless constructor to be used with [Intercept].</summary>
public sealed class CountingInterceptor : IInterceptor
{
    public static int Calls;

    public void Intercept(IInvocation invocation)
    {
        Interlocked.Increment(ref Calls);
        invocation.Proceed();
    }
}
