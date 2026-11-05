namespace Maxen.DI.Tests.Fixtures;

/// <summary>Tallies how many graph nodes were created and disposed during one randomized test.</summary>
public sealed class GraphCounters
{
    private int _created;
    private int _disposed;
    private int _disposedTwice;

    public int CreatedCount => Volatile.Read(ref _created);
    public int DisposedCount => Volatile.Read(ref _disposed);
    public int DisposedTwiceCount => Volatile.Read(ref _disposedTwice);

    public void Created() => Interlocked.Increment(ref _created);
    public void Disposed() => Interlocked.Increment(ref _disposed);
    public void DisposedTwice() => Interlocked.Increment(ref _disposedTwice);
}
