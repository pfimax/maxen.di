namespace Maxen.DI.Tests.Fixtures;

/// <summary>A plain consumer of IClock that has no special treatment.</summary>
public class NeedsClock
{
    public NeedsClock(IClock clock) => Clock = clock;

    public IClock Clock { get; }
}
