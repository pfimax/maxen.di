using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Maxen.DI.Internal;

/// <summary>
/// Works out which key a constructor parameter asks for. Besides Maxen's own <see cref="FromKeyAttribute"/>, it reads
/// Microsoft's <c>[FromKeyedServices]</c> and <c>[ServiceKey]</c> by name. Classes written for Microsoft's container then
/// work here unchanged, and the core library still takes no dependency on Microsoft's packages.
/// </summary>
internal static class ParameterKeys
{
    private const string FromKeyedServicesName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";
    private const string ServiceKeyName = "Microsoft.Extensions.DependencyInjection.ServiceKeyAttribute";

    /// <summary>"Use the key of the service being built": <c>[FromKeyedServices]</c> with no key.</summary>
    public static readonly object Inherit = new KeyMarker("the key of the service being built");

    /// <summary>"Hand me the key itself, this is not a service": <c>[ServiceKey]</c>.</summary>
    public static readonly object OwnKey = new KeyMarker("the service key itself");

    // Attribute reads allocate and the answers never change, so both are worked out once.
    private static readonly ConcurrentDictionary<ParameterInfo, ServiceKey> Declared = new();
    private static readonly ConcurrentDictionary<Type, bool> OwnerDependent = new();

    /// <summary>
    /// The key as written on the parameter. It may hold <see cref="Inherit"/> or <see cref="OwnKey"/>, which only become
    /// a real answer once we know which registration is being built; see <see cref="ForOwner"/>.
    /// </summary>
    public static ServiceKey Of(ParameterInfo parameter)
    {
        if (Declared.TryGetValue(parameter, out var key)) return key;
        return Declared[parameter] = Read(parameter);
    }

    /// <summary>True for <c>[ServiceKey]</c> parameters, which receive the key instead of a service.</summary>
    public static bool IsOwnKey(ServiceKey declared) => ReferenceEquals(declared.Key, OwnKey);

    /// <summary>The key to look the parameter's service up with, for a parameter of the registration keyed <paramref name="ownerKey"/>.</summary>
    public static ServiceKey ForOwner(ServiceKey declared, object? ownerKey)
        => ReferenceEquals(declared.Key, Inherit) ? new ServiceKey(declared.Type, ownerKey) : declared;

    /// <summary>
    /// Whether a <c>[ServiceKey]</c> parameter of type <paramref name="parameterType"/> can take <paramref name="ownerKey"/>.
    /// An unkeyed service has no key to give. <see cref="ServiceKey.AnyKey"/> only shows up while the any-key registration
    /// itself is inspected (validation, graphs), before any real key is known, so it gets the benefit of the doubt.
    /// </summary>
    public static bool AcceptsOwnKey(Type parameterType, object? ownerKey)
        => ownerKey is not null && (ReferenceEquals(ownerKey, ServiceKey.AnyKey) || parameterType.IsInstanceOfType(ownerKey));

    /// <summary>
    /// True when some constructor of <paramref name="type"/> has a parameter whose answer depends on the registration's
    /// key. Such types can't share one cached constructor choice: the same class may be registered under several keys,
    /// or unkeyed, and <c>[ServiceKey]</c> decides which constructors are usable at all.
    /// </summary>
    public static bool DependsOnOwnerKey([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        if (OwnerDependent.TryGetValue(type, out var known)) return known;

        var dependent = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Any(p => Of(p).Key is { } key && (ReferenceEquals(key, Inherit) || ReferenceEquals(key, OwnKey)));
        return OwnerDependent[type] = dependent;
    }

    private static ServiceKey Read(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;

        var fromKey = parameter.GetCustomAttribute<FromKeyAttribute>();
        if (fromKey is not null) return new ServiceKey(type, fromKey.Key);

        // Attribute data rather than instances: reading the attribute's own properties would need reflection the trimmer can't see.
        foreach (var data in parameter.GetCustomAttributesData())
        {
            var name = data.AttributeType.FullName;
            if (name == ServiceKeyName) return new ServiceKey(type, OwnKey);
            if (name != FromKeyedServicesName) continue;

            // No argument means "inherit the key" (added in version 10 of Microsoft's packages). With one, it is the key,
            // and a null key means unkeyed.
            return data.ConstructorArguments.Count == 0
                ? new ServiceKey(type, Inherit)
                : new ServiceKey(type, ValueOf(data.ConstructorArguments[0]));
        }

        return new ServiceKey(type);
    }

    // Attribute data stores an enum constant as its underlying number. The key has to be the enum value itself,
    // or it would never equal the key the service was registered with.
    private static object? ValueOf(CustomAttributeTypedArgument argument)
        => argument.ArgumentType.IsEnum && argument.Value is not null
            ? Enum.ToObject(argument.ArgumentType, argument.Value)
            : argument.Value;
}
