using Maxen.DI.Interception;

namespace Maxen.DI.Samples.Generators.Interception;

public sealed class AuditTrail : IInterceptor
{
    public static readonly List<string> Entries = new();

    public void Intercept(IInvocation invocation)
    {
        Entries.Add(invocation.Method.Name);
        invocation.Proceed();
    }
}
