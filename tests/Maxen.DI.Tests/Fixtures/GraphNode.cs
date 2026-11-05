namespace Maxen.DI.Tests.Fixtures;

/// <summary>One service in a randomly generated graph. Counts how often it is disposed, so tests can catch leaks and double disposal.</summary>
public sealed class GraphNode : IDisposable
{
    private readonly GraphCounters _counters;
    private int _disposed;

    public GraphNode(int id, GraphNode[] children, GraphCounters counters)
    {
        Id = id;
        Children = children;
        _counters = counters;
        counters.Created();
    }

    public int Id { get; }

    public GraphNode[] Children { get; }

    public void Dispose()
    {
        // A second call would mean the container disposed something twice.
        if (Interlocked.Exchange(ref _disposed, 1) == 1) _counters.DisposedTwice();
        else _counters.Disposed();
    }
}
