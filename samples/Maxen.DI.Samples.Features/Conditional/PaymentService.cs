namespace Maxen.DI.Samples.Features.Conditional;

internal sealed class PaymentService
{
    public PaymentService(ILogSink sink) => Sink = sink;

    public ILogSink Sink { get; }
}
