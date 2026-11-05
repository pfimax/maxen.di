using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>
/// Decides how long a created instance lives. Derive from this to add your own lifetimes
/// (per thread, per request, pooled, ...) and attach it with <c>WithLifetime</c>.
/// </summary>
public abstract class Lifetime
{
    /// <summary>A new instance every time.</summary>
    public static Lifetime Transient { get; } = new TransientLifetime();
    /// <summary>One instance per scope.</summary>
    public static Lifetime Scoped { get; } = new ScopedLifetime();
    /// <summary>One instance for the whole container.</summary>
    public static Lifetime Singleton { get; } = new SingletonLifetime();

    /// <summary>Turns the public enum used by <c>[Service]</c> into the matching lifetime.</summary>
    public static Lifetime From(ServiceLifetime kind) => kind switch
    {
        ServiceLifetime.Transient => Transient,
        ServiceLifetime.Scoped => Scoped,
        ServiceLifetime.Singleton => Singleton,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>A short name used in messages and diagrams.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// True when instances belong to some narrower context than the whole container (a scope, a
    /// thread, a lease). Longer-lived services must not capture these, otherwise they would keep
    /// using an instance after its context has ended.
    /// </summary>
    public virtual bool IsScopeBound => false;

    /// <summary>Whether a service with this lifetime may safely depend on one with <paramref name="dependency"/>.</summary>
    public virtual bool CanDependOn(Lifetime dependency) => true;

    /// <summary>Returns a cached instance, or calls <paramref name="next"/> to build a new one.</summary>
    public abstract object Resolve(ResolveContext context, ResolveDelegate next);

    /// <inheritdoc/>
    public override string ToString() => Name;
}
