using Maxen.DI.Internal;

namespace Maxen.DI;

/// <summary>
/// Identifies a service: its type plus an optional key. The key is what lets you register
/// several implementations of the same interface and pick one by name.
/// </summary>
public readonly record struct ServiceKey(Type Type, object? Key = null)
{
    /// <summary>
    /// Stands for every key at once. Register a service with it and that registration answers any key that has no
    /// registration of its own. Each key is treated as a registration of its own, so a singleton registered this way gives
    /// one instance per key: <c>Resolve&lt;ITenantDb&gt;("acme")</c> and <c>Resolve&lt;ITenantDb&gt;("globex")</c> return two
    /// different databases, and a <c>[ServiceKey]</c> constructor parameter tells each one which key it was made for.
    /// Resolve <c>IEnumerable&lt;T&gt;</c> with it to get every keyed registration of <c>T</c> (the catch-all registrations
    /// themselves are left out). Resolving a single service with it throws, because there is no one service to return.
    /// Microsoft's <c>KeyedService.AnyKey</c> means the same and is translated to this by
    /// <c>Maxen.DI.Extensions.DependencyInjection</c>.
    /// </summary>
    public static object AnyKey { get; } = new KeyMarker("any key");

    /// <summary>True when this identifies a keyed registration.</summary>
    public bool IsKeyed => Key is not null;

    /// <summary>True when the key is <see cref="AnyKey"/>.</summary>
    public bool IsAnyKey => ReferenceEquals(Key, AnyKey);

    /// <summary>Shorthand for <c>new ServiceKey(typeof(T), key)</c>.</summary>
    public static ServiceKey For<T>(object? key = null) => new(typeof(T), key);

    // Readable output matters here: this ends up in exception messages.
    /// <inheritdoc/>
    public override string ToString() =>
        Key is null ? TypeNames.Format(Type) : $"{TypeNames.Format(Type)} (key: {Key})";
}
