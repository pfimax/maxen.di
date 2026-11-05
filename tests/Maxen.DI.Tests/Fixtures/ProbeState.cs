namespace Maxen.DI.Tests.Fixtures;

/// <summary>Shared singleton the hosted service writes into, so a test can see that it ran.</summary>
public sealed class ProbeState
{
    public volatile bool Started;
    public volatile bool Stopped;
}
