namespace Maxen.DI.Samples.Features.Conditional;

internal sealed class AuditSink : ILogSink
{
    public string Name => "audit sink (tamper-proof)";
}
