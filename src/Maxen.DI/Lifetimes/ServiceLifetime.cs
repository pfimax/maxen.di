namespace Maxen.DI;

/// <summary>
/// The three built-in lifetimes as a plain enum. It exists because attributes can't take
/// <see cref="Lifetime"/> objects as arguments.
/// </summary>
public enum ServiceLifetime
{
    /// <summary>A new instance every time.</summary>
    Transient,

    /// <summary>One instance per scope.</summary>
    Scoped,

    /// <summary>One instance for the whole container.</summary>
    Singleton,
}
