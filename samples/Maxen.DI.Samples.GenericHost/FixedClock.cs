namespace Maxen.DI.Samples.GenericHost;

/// <summary>Decorates the real clock so the sample prints the same time on every run.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(IClock inner)
    {
        Inner = inner;
    }

    public IClock Inner { get; }

    public DateTime Now => new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}
