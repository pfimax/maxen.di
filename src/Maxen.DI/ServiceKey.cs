using Maxen.DI.Internal;

namespace Maxen.DI;

/// <summary>
/// Identifies a service: its type plus an optional key. The key is what lets you register
/// several implementations of the same interface and pick one by name.
/// </summary>
public readonly record struct ServiceKey(Type Type, object? Key = null)
{
    /// <summary>True when this identifies a keyed registration.</summary>
    public bool IsKeyed => Key is not null;

    /// <summary>Shorthand for <c>new ServiceKey(typeof(T), key)</c>.</summary>
    public static ServiceKey For<T>(object? key = null) => new(typeof(T), key);

    // Readable output matters here: this ends up in exception messages.
    /// <inheritdoc/>
    public override string ToString() =>
        Key is null ? TypeNames.Format(Type) : $"{TypeNames.Format(Type)} (key: {Key})";
}
