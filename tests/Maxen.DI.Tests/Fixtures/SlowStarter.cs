using Maxen.DI.Async;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>A service that needs async setup. Counts how often that setup ran.</summary>
public class SlowStarter : IAsyncInitializable
{
    private int _attempts;

    public int InitializeCalls => _attempts;
    public bool Ready { get; private set; }

    /// <summary>When set, the first attempt fails, to test retrying.</summary>
    public bool FailFirstAttempt { get; set; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _attempts);
        await Task.Delay(20, cancellationToken);

        if (FailFirstAttempt && attempt == 1)
            throw new InvalidOperationException("first attempt fails");

        Ready = true;
    }
}
