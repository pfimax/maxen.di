namespace Maxen.DI.Tests.Fixtures;

/// <summary>Asks for the clock registered under the key "fast".</summary>
public class NeedsFastClock
{
    public NeedsFastClock([FromKey("fast")] IClock clock) => Clock = clock;

    public IClock Clock { get; }
}
