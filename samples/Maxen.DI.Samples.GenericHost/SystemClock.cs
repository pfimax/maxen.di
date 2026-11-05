namespace Maxen.DI.Samples.GenericHost;

public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}
