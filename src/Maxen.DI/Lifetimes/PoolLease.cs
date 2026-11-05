namespace Maxen.DI;

/// <summary>
/// Handed to the scope in place of the instance itself, so that when the scope ends the instance
/// goes back to its pool instead of being disposed.
/// </summary>
internal sealed class PoolLease : IDisposable
{
    private readonly InstancePool _pool;
    private readonly object _instance;

    public PoolLease(InstancePool pool, object instance)
    {
        _pool = pool;
        _instance = instance;
    }

    public void Dispose() => _pool.Return(_instance);
}
