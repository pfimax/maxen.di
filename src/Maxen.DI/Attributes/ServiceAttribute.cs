namespace Maxen.DI;

/// <summary>Declares how a class is registered, so <c>RegisterAttributed</c> can pick it up from an assembly.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceAttribute : Attribute
{
    /// <summary>Marks the class for registration with the given lifetime.</summary>
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Transient) => Lifetime = lifetime;

    /// <summary>How long instances of the class live.</summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>The service type to register as. Defaults to the class itself.</summary>
    public Type? ServiceType { get; set; }

    /// <summary>Optional key to register the class under, for keyed lookups.</summary>
    public object? Key { get; set; }
}
