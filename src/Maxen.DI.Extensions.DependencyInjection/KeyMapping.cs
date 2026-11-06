using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>
/// Translates Microsoft's "any key" into Maxen's and back. Both mean the same thing, but they are different objects,
/// and keys are compared by equality, so every key crossing between the two worlds goes through here.
/// </summary>
internal static class KeyMapping
{
    public static object? ToMaxen(object? key) => ReferenceEquals(key, KeyedService.AnyKey) ? ServiceKey.AnyKey : key;

    public static object? ToMicrosoft(object? key) => ReferenceEquals(key, ServiceKey.AnyKey) ? KeyedService.AnyKey : key;
}
