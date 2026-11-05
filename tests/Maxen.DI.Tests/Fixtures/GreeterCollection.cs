namespace Maxen.DI.Tests.Fixtures;

/// <summary>Takes every registered greeter via a collection parameter.</summary>
public class GreeterCollection
{
    public GreeterCollection(IEnumerable<IGreeter> all) => All = all.ToList();

    public IReadOnlyList<IGreeter> All { get; }
}
