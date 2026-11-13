using Maxen.DI.Extensibility;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Supplies <see cref="FlakyDependency"/>, but throws the first time it is asked for it.</summary>
public sealed class FlakySource : IRegistrationSource
{
    private int _asked;

    public IEnumerable<ServiceDescriptor> GetRegistrations(ServiceKey key, IContainer container)
    {
        if (key.Type != typeof(FlakyDependency)) return Array.Empty<ServiceDescriptor>();

        if (Interlocked.Increment(ref _asked) == 1)
            throw new InvalidOperationException("The source was not ready yet.");

        return new[] { ServiceDescriptor.ForType(typeof(FlakyDependency), typeof(FlakyDependency)) };
    }
}
