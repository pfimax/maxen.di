namespace Maxen.DI.Tests.Fixtures;

/// <summary>Calls back when disposed, so tests can count how many instances were cleaned up.</summary>
public sealed class DisposeCounter : IDisposable
{
    private readonly Action _onDispose;

    public DisposeCounter(Action onDispose) => _onDispose = onDispose;

    public void Dispose() => _onDispose();
}
