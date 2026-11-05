using System.Collections.Concurrent;

namespace Maxen.DI;

/// <summary>
/// Holds the instances a scope has created, and disposes them (newest first) when the scope ends.
/// Custom lifetimes use this to cache and to hand over disposal duty.
/// </summary>
public sealed class InstanceStore : IDisposable, IAsyncDisposable
{
    // Reading is the hot path (every resolve of an existing instance), so lookups are lock-free.
    // Only creation takes the lock. Doing the creation inside it on purpose means a singleton is
    // never built twice, and the lock is re-entrant so nested resolves on the same store are fine.
    private readonly object _gate = new();

    // Most scopes hold only a handful of instances, so a tiny copy-on-write array scanned by reference
    // beats hashing: reads need no lock and allocate nothing. Past SmallLimit we switch to a dictionary.
    private const int SmallLimit = 12;
    // The array has spare room and _count says how much of it is published, so adding an entry usually
    // writes into a free slot instead of copying. A reader only trusts slots below the count it saw; a
    // stale read can only cause a miss, and every miss is re-checked under the lock before creating.
    private const int InitialCapacity = 4;
    private (object Key, object Value)[] _small = Array.Empty<(object, object)>();
    private int _count;
    private ConcurrentDictionary<object, object>? _large;
    private List<object>? _disposables;
    private volatile bool _disposed;

    /// <summary>
    /// Returns the cached instance for <paramref name="key"/>, creating it first if needed.
    /// When <paramref name="owned"/> is true the store disposes the instance along with itself.
    /// </summary>
    public object GetOrCreate(object key, Func<object> create, bool owned)
    {
        if (TryGetCached(key, out var cached))
            return cached!;

        lock (_gate)
        {
            ThrowIfDisposed();

            // Someone may have created it while we waited for the lock.
            if (TryGetCached(key, out cached))
                return cached!;

            var instance = create();
            Add(key, instance);
            if (owned) TrackCore(instance);
            return instance;
        }
    }

    /// <summary>
    /// Same as <see cref="GetOrCreate(object, Func{object}, bool)"/>, but the data <paramref name="create"/> needs is passed in
    /// rather than captured, so a static lambda can be used and nothing is allocated for the call.
    /// </summary>
    public object GetOrCreate<TState>(object key, TState state, Func<TState, object> create, bool owned)
    {
        if (TryGetCached(key, out var cached))
            return cached!;

        lock (_gate)
        {
            ThrowIfDisposed();

            if (TryGetCached(key, out cached))
                return cached!;

            var instance = create(state);
            Add(key, instance);
            if (owned) TrackCore(instance);
            return instance;
        }
    }

    /// <summary>Looks an instance up without locking. Throws if the store has been disposed.</summary>
    public bool TryGet(object key, out object? instance)
    {
        ThrowIfDisposed();
        return TryGetCached(key, out instance);
    }

    private bool TryGetCached(object key, out object? instance)
    {
        // Read the array once, then only the slots below the published count (see the field comments).
        var small = Volatile.Read(ref _small);
        var count = Math.Min(Volatile.Read(ref _count), small.Length);
        for (var i = 0; i < count; i++)
        {
            var entry = small[i];
            if (ReferenceEquals(entry.Key, key) || entry.Key.Equals(key))
            {
                instance = entry.Value;
                return true;
            }
        }

        var large = Volatile.Read(ref _large);
        if (large is not null && large.TryGetValue(key, out var found))
        {
            instance = found;
            return true;
        }

        instance = null;
        return false;
    }

    /// <summary>Stores a new entry. Only called while holding the lock.</summary>
    private void Add(object key, object instance)
    {
        if (_large is not null)
        {
            _large[key] = instance;
            return;
        }

        var current = _small;
        var count = _count;
        if (count >= SmallLimit)
        {
            // Too many for a linear scan: move everything into a dictionary.
            var large = new ConcurrentDictionary<object, object>();
            for (var i = 0; i < count; i++) large[current[i].Key] = current[i].Value;
            large[key] = instance;
            Volatile.Write(ref _large, large);
            Volatile.Write(ref _count, 0);
            Volatile.Write(ref _small, Array.Empty<(object, object)>());
            return;
        }

        if (count == current.Length)
        {
            // Out of room. Most scopes hold exactly one scoped service, so the first array is sized for one; after
            // that we jump to a few slots and then double, and publish the bigger array before filling it.
            var grown = new (object Key, object Value)[Math.Min(SmallLimit, current.Length == 0 ? 1 : Math.Max(InitialCapacity, current.Length * 2))];
            Array.Copy(current, grown, count);
            Volatile.Write(ref _small, grown);
            current = grown;
        }

        // Fill the slot before announcing it through the count.
        current[count] = (key, instance);
        Volatile.Write(ref _count, count + 1);
    }

    /// <summary>Marks an instance for disposal with this store. Objects that aren't disposable are ignored.</summary>
    public void Track(object instance)
    {
        if (!IsDisposable(instance)) return;
        lock (_gate)
        {
            ThrowIfDisposed();
            TrackCore(instance);
        }
    }

    private void TrackCore(object instance)
    {
        if (IsDisposable(instance))
            (_disposables ??= new()).Add(instance);
    }

    private static bool IsDisposable(object instance) => instance is IDisposable or IAsyncDisposable;

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(InstanceStore), "The scope has already been disposed.");
    }

    /// <summary>Marks the store disposed and returns what needs disposing, newest first.</summary>
    private List<object>? Take()
    {
        lock (_gate)
        {
            if (_disposed) return null;
            _disposed = true;

            var list = _disposables;
            _disposables = null;
            _large?.Clear();
            _small = Array.Empty<(object, object)>();
            _count = 0;
            list?.Reverse();
            return list;
        }
    }

    /// <summary>Disposes everything the store owns, newest first, and closes the store.</summary>
    public void Dispose()
    {
        var list = Take();
        if (list is null) return;

        // Keep going when one dispose fails; the rest still deserve a chance to clean up.
        List<Exception>? errors = null;
        foreach (var item in list)
        {
            try
            {
                if (item is IDisposable d) d.Dispose();
                else ((IAsyncDisposable)item).DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex) { (errors ??= new()).Add(ex); }
        }
        if (errors is not null) throw new AggregateException(errors);
    }

    /// <summary>Asynchronously disposes everything the store owns, newest first, and closes the store.</summary>
    public async ValueTask DisposeAsync()
    {
        var list = Take();
        if (list is null) return;

        List<Exception>? errors = null;
        foreach (var item in list)
        {
            try
            {
                if (item is IAsyncDisposable ad) await ad.DisposeAsync().ConfigureAwait(false);
                else ((IDisposable)item).Dispose();
            }
            catch (Exception ex) { (errors ??= new()).Add(ex); }
        }
        if (errors is not null) throw new AggregateException(errors);
    }
}
