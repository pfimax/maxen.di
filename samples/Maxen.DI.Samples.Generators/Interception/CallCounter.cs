using Maxen.DI.Interception;

namespace Maxen.DI.Samples.Generators.Interception;

/// <summary>Needs a public parameterless constructor, because the generated proxy creates it itself.</summary>
public sealed class CallCounter : IInterceptor
{
    public static int Calls;

    public void Intercept(IInvocation invocation)
    {
        Interlocked.Increment(ref Calls);
        invocation.Proceed();
    }
}
