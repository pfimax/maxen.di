using Maxen.DI.Extensibility;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>A custom registration source that conjures up <see cref="AutoMade"/> on demand.</summary>
public sealed class AutoMadeSource : IRegistrationSource
{
    public IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container)
    {
        if (key.Type == typeof(AutoMade))
            yield return ServiceDescriptor.ForFactory(key.Type, _ => new AutoMade());
    }
}
