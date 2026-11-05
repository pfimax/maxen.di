namespace Maxen.DI.Tests.Fixtures;

/// <summary>Takes every registered clock through a collection parameter.</summary>
public class ClockCollection
{
    public ClockCollection(IEnumerable<IClock> all) => All = all.ToList();

    public IReadOnlyList<IClock> All { get; }
}
