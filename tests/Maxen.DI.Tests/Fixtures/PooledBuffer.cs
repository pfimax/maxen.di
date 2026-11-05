namespace Maxen.DI.Tests.Fixtures;

/// <summary>A pooled service that tracks resets and disposal.</summary>
public class PooledBuffer : IPoolable, IDisposable
{
    public int Resets { get; private set; }
    public bool Disposed { get; private set; }

    public void Reset() => Resets++;

    public void Dispose() => Disposed = true;
}
