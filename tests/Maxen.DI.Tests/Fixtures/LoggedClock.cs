namespace Maxen.DI.Tests.Fixtures;

/// <summary>A decorator with a dependency of its own, besides the clock it wraps.</summary>
public class LoggedClock : IClock
{
    public LoggedClock(IClock inner, Disposable log)
    {
        Inner = inner;
        Log = log;
    }

    public IClock Inner { get; }
    public Disposable Log { get; }
}
