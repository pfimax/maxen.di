namespace Maxen.DI;

/// <summary>Asks for the keyed registration of a service, on a constructor parameter or an injected property.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class FromKeyAttribute : Attribute
{
    /// <summary>Asks for the registration made with the given key.</summary>
    public FromKeyAttribute(object key) => Key = key;

    /// <summary>The key the parameter should be resolved with.</summary>
    public object Key { get; }
}
