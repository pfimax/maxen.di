namespace Maxen.DI.Samples.Features.Lifetimes;

/// <summary>Reused between scopes; <see cref="Reset"/> runs before it is handed out again.</summary>
internal sealed class PooledBuffer : IPoolable
{
    private static int _next;

    public int Id { get; } = Interlocked.Increment(ref _next);

    public int Uses { get; private set; }

    public void Reset() => Uses++;
}
