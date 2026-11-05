namespace Maxen.DI.Extensibility;

/// <summary>
/// A reusable bundle of configuration. A module can register services and plug into any
/// extension point, which makes it a good unit for sharing setup between applications.
/// </summary>
public interface IContainerModule
{
    /// <summary>Adds this module's registrations to the builder.</summary>
    void Configure(ContainerBuilder builder);
}
