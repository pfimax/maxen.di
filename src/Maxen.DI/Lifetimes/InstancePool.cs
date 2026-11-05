using System.Collections.Concurrent;

namespace Maxen.DI;

/// <summary>The stash of idle instances behind a <see cref="PooledLifetime"/>.</summary>
internal sealed class InstancePool : IDisposable
{
    private readonly ConcurrentBag<object> _idle = new();
    private readonly int _maxRetained;
    private int _retained;
    private volatile bool _disposed;

    public InstancePool(int maxRetained) => _maxRetained = maxRetained;

    /// <summary>Takes an idle instance if there is one, otherwise returns null and the caller builds a new one.</summary>
    public object? Rent()
    {
        if (!_idle.TryTake(out var instance)) return null;

        Interlocked.Decrement(ref _retained);
        return instance;
    }

    /// <summary>Puts an instance back. If the pool is full or already shut down, the instance is disposed instead.</summary>
    public void Return(object instance)
    {
        if (instance is IPoolable poolable)
            poolable.Reset();

        // Reserve a slot first and give it back if we lost the race, so the limit holds under contention.
        if (!_disposed && Interlocked.Increment(ref _retained) <= _maxRetained)
        {
            _idle.Add(instance);

            // Disposal may have started while we were adding; make sure nothing is left behind.
            if (_disposed) DisposeIdle();
            return;
        }

        Interlocked.Decrement(ref _retained);
        DisposeInstance(instance);
    }

    public void Dispose()
    {
        _disposed = true;
        DisposeIdle();
    }

    private void DisposeIdle()
    {
        while (_idle.TryTake(out var instance))
        {
            Interlocked.Decrement(ref _retained);
            DisposeInstance(instance);
        }
    }

    private static void DisposeInstance(object instance)
    {
        if (instance is IDisposable disposable) disposable.Dispose();
        else if (instance is IAsyncDisposable asyncDisposable) asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
