namespace Maxen.DI.Samples.Features.Conditional;

internal sealed class ReportService
{
    public ReportService(ILogSink sink) => Sink = sink;

    public ILogSink Sink { get; }
}
