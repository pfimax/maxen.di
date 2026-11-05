namespace Maxen.DI.Samples.Features.Deferred;

/// <summary>Needs a value only the caller knows (the id) next to a service the container provides.</summary>
internal sealed class Order
{
    public Order(int id, ExpensiveReport report)
    {
        Id = id;
        Report = report;
    }

    public int Id { get; }

    public ExpensiveReport Report { get; }
}
