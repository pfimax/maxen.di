using Maxen.DI.Async;

namespace Maxen.DI.Samples.Features.AsyncInit;

/// <summary>Needs an awaited step before it is usable, which a constructor cannot do.</summary>
internal sealed class Connection : IAsyncInitializable
{
    public bool IsOpen { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(20, cancellationToken);
        IsOpen = true;
    }
}
