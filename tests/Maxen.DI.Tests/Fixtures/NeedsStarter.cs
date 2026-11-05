using Maxen.DI.Async;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Depends on <see cref="SlowStarter"/> and records whether it was ready when this one was initialized.</summary>
public class NeedsStarter : IAsyncInitializable
{
    private readonly SlowStarter _starter;

    public NeedsStarter(SlowStarter starter) => _starter = starter;

    public bool StarterWasReady { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        StarterWasReady = _starter.Ready;
        return Task.CompletedTask;
    }
}
