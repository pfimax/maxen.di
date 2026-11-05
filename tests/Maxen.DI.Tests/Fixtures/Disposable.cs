namespace Maxen.DI.Tests.Fixtures;

/// <summary>Remembers whether it has been disposed.</summary>
public class Disposable : IDisposable
{
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}
