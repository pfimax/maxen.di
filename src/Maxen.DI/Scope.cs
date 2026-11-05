using Maxen.DI.Arguments;

namespace Maxen.DI;

/// <summary>A unit of work with its own scoped instances, created from the container or from another scope.</summary>
public class Scope : IScope
{
    // Null for the root container, which is its own root.
    private readonly Container? _root;

    // A scope that never resolves anything scoped shouldn't pay for a store, so it's made on first touch.
    private InstanceStore? _instances;

    // Only filled when DisposeChildScopes is on: the live scopes created from this one.
    private List<Scope>? _children;

    private static readonly List<Scope> ClosedChildren = new();

    internal Scope(Container? root, Scope? parent = null, object? tag = null)
    {
        _root = root;
        ParentScope = parent;
        Tag = tag;
    }

    /// <summary>The scope (or container) this one was created from; null only for the root container itself.</summary>
    internal Scope? ParentScope { get; }

    /// <inheritdoc/>
    public object? Tag { get; }

    internal Container Core => _root ?? (Container)this;

    /// <inheritdoc/>
    public IContainer Root => Core;
    /// <inheritdoc/>
    public bool IsRoot => _root is null;
    /// <inheritdoc/>
    public InstanceStore Instances
    {
        get
        {
            // Two threads may race here; CompareExchange makes sure they both end up with the same store.
            var store = Volatile.Read(ref _instances);
            if (store is not null) return store;

            Interlocked.CompareExchange(ref _instances, new InstanceStore(), null);
            return _instances!;
        }
    }

    // Every scope owns its instances separately, whatever it was created from, so nesting adds no sharing
    // of ordinary scoped services. The parent link exists only so tagged lifetimes can find "the nearest
    // scope with this label". Disposing a parent does not dispose its children; each owner disposes its own.
    /// <inheritdoc/>
    public IScope CreateScope() => Adopt(new Scope(Core, this));

    /// <inheritdoc/>
    public IScope CreateScope(object tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return Adopt(new Scope(Core, this, tag));
    }

    // With DisposeChildScopes on, remember the child so disposing us can dispose it too.
    private Scope Adopt(Scope child)
    {
        if (!Core.Options.DisposeChildScopes) return child;

        var list = Volatile.Read(ref _children) ?? Interlocked.CompareExchange(ref _children, new List<Scope>(), null) ?? _children!;
        lock (list)
        {
            // A scope that was already disposed can't take on new children without leaking them.
            if (ReferenceEquals(list, ClosedChildren))
                throw new ObjectDisposedException(nameof(Scope), "The scope has already been disposed.");
            if (ReferenceEquals(Volatile.Read(ref _children), ClosedChildren))
                throw new ObjectDisposedException(nameof(Scope), "The scope has already been disposed.");
            list.Add(child);
        }
        
        return child;
    }

    // A child that is disposed on its own shouldn't stay on the parent's list.
    private void Forget(Scope child)
    {
        var list = Volatile.Read(ref _children);
        if (list is null || ReferenceEquals(list, ClosedChildren)) return;
        lock (list) list.Remove(child);
    }

    // Takes the children away for good, newest first, so the caller can dispose them outside any lock.
    private Scope[] CloseChildren()
    {
        var list = Interlocked.Exchange(ref _children, ClosedChildren);
        if (list is null || ReferenceEquals(list, ClosedChildren)) return Array.Empty<Scope>();
        lock (list)
        {
            var kids = list.ToArray();
            Array.Reverse(kids);
            return kids;
        }
    }

    /// <summary>The nearest scope (this one first, then its parents) that carries the label, or null.</summary>
    internal Scope? FindTagged(object tag)
    {
        for (Scope? scope = this; scope is not null; scope = scope.ParentScope)
            if (scope.Tag is not null && scope.Tag.Equals(tag))
                return scope;
        return null;
    }

    /// <inheritdoc/>
    public bool IsRegistered(ServiceKey key) => Core.IsRegisteredCore(key);

    /// <inheritdoc/>
    public object Resolve(ServiceKey key) => Core.ResolveCore(key, this, null, required: true)!;
    /// <inheritdoc/>
    public object ResolveWith(ServiceKey key, ResolveArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return Core.ResolveCore(key, this, null, required: true, arguments)!;
    }

    /// <inheritdoc/>
    public object? TryResolve(ServiceKey key) => Core.ResolveCore(key, this, null, required: false);
    /// <inheritdoc/>
    public IReadOnlyList<object> ResolveAll(ServiceKey key) => Core.ResolveAllCore(key, this, null);

    // IServiceProvider says "return null if unknown", so GetService never throws for missing services.
    object? IServiceProvider.GetService(Type serviceType) => TryResolve(new ServiceKey(serviceType));

    // After disposal the scope points at one shared, already-disposed store, so resolving a scoped
    // service from a dead scope still throws ObjectDisposedException without us allocating anything.
    private static readonly InstanceStore DisposedStore = CreateDisposedStore();

    private static InstanceStore CreateDisposedStore()
    {
        var store = new InstanceStore();
        store.Dispose();
        return store;
    }

    /// <summary>Disposes everything this scope built, and, when <c>DisposeChildScopes</c> is on, its child scopes first.</summary>
    public void Dispose()
    {
        List<Exception>? errors = null;

        // Children first: they may still be using things this scope owns.
        foreach (var child in CloseChildren())
        {
            try { child.Dispose(); }
            catch (Exception ex) { Collect(ref errors, ex); }
        }

        try { Interlocked.Exchange(ref _instances, DisposedStore)?.Dispose(); }
        catch (Exception ex) { Collect(ref errors, ex); }

        ParentScope?.Forget(this);
        if (errors is not null) throw new AggregateException(errors);
    }

    /// <summary>Asynchronous version of <see cref="Dispose"/>.</summary>
    public async ValueTask DisposeAsync()
    {
        List<Exception>? errors = null;

        foreach (var child in CloseChildren())
        {
            try { await child.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { Collect(ref errors, ex); }
        }

        var store = Interlocked.Exchange(ref _instances, DisposedStore);
        if (store is not null)
        {
            try { await store.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { Collect(ref errors, ex); }
        }

        ParentScope?.Forget(this);
        if (errors is not null) throw new AggregateException(errors);
    }

    // Keeps one flat AggregateException instead of nesting a new one per level of scopes.
    private static void Collect(ref List<Exception>? errors, Exception ex)
    {
        errors ??= new();
        if (ex is AggregateException aggregate) errors.AddRange(aggregate.InnerExceptions);
        else errors.Add(ex);
    }
}
