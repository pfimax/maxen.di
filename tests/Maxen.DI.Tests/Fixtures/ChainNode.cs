namespace Maxen.DI.Tests.Fixtures;

/// <summary>Base of the six real classes used by the exhaustive plan comparison. Remembers its children and how often it was disposed.</summary>
public abstract class ChainNode : IDisposable
{
    protected ChainNode(params ChainNode[] children) => Children = children;

    public ChainNode[] Children { get; }

    public int DisposeCount { get; private set; }

    public void Dispose() => DisposeCount++;
}
