namespace Maxen.DI.Samples.Features.Deferred;

internal sealed class Dashboard
{
    public Dashboard(Lazy<ExpensiveReport> report, Func<ExpensiveReport> newReport, Func<int, Order> orderFactory)
    {
        Report = report;
        NewReport = newReport;
        OrderFactory = orderFactory;
    }

    public Lazy<ExpensiveReport> Report { get; }

    public Func<ExpensiveReport> NewReport { get; }

    public Func<int, Order> OrderFactory { get; }
}
