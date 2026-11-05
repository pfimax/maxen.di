namespace Maxen.DI.Samples.Features.Conditional;

internal sealed class ConditionalSample : ISample
{
    public string Name => "conditional";
    public string Summary => "pick the implementation by who is asking (WhenInjectedInto, When)";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<ILogSink, ConsoleSink>();
        // Later registrations win, so the specific rule comes after the default.
        builder.Register<ILogSink, AuditSink>().WhenInjectedInto<PaymentService>();
        builder.Register<PaymentService>();
        builder.Register<ReportService>();

        using var container = builder.Build();
        Console.WriteLine("payments use: " + container.Resolve<PaymentService>().Sink.Name);
        Console.WriteLine("reports use:  " + container.Resolve<ReportService>().Sink.Name);
        return Task.CompletedTask;
    }
}
