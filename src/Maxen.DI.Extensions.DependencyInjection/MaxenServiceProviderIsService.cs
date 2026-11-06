using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>Answers "could this type be resolved?", which frameworks like minimal APIs use to decide what to inject.</summary>
internal sealed class MaxenServiceProviderIsService : IServiceProviderIsService, IServiceProviderIsKeyedService
{
    private readonly IContainer _container;

    public MaxenServiceProviderIsService(IContainer container) => _container = container;

    public bool IsService(Type serviceType)
        // An open generic like IEnumerable<> is a recipe, not something you can ask for.
        => !serviceType.IsGenericTypeDefinition && _container.IsRegistered(new ServiceKey(serviceType));

    public bool IsKeyedService(Type serviceType, object? serviceKey)
        => !serviceType.IsGenericTypeDefinition && _container.IsRegistered(new ServiceKey(serviceType, KeyMapping.ToMaxen(serviceKey)));
}
