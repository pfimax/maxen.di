namespace Maxen.DI.Extensibility;

/// <summary>
/// Supplies registrations on the fly for keys nobody registered explicitly: collections,
/// <c>Lazy&lt;T&gt;</c>, auto-generated factories, mocks and so on.
/// The descriptors you return must carry exactly the key that was asked for.
/// </summary>
public interface IRegistrationSource
{
    /// <summary>Offers registrations for a key nobody registered explicitly; return none if the key is not yours.</summary>
    IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container);
}
