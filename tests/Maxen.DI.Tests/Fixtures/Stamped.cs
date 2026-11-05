namespace Maxen.DI.Tests.Fixtures;

/// <summary>Tells tests whether it was built by a generated constructor call instead of the container's own.</summary>
public class Stamped
{
    public Stamped(IClock clock) => Clock = clock;

    public IClock Clock { get; }

    public bool ViaGenerator { get; init; }
}
