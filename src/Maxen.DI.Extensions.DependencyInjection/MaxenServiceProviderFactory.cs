using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>
/// Plugs Maxen.DI into the Generic Host / ASP.NET Core. Everything registered in the Microsoft
/// <see cref="IServiceCollection"/> is carried over, and you can add Maxen-specific configuration
/// (decorators, interceptors, extra sources) through the optional callback.
/// </summary>
public sealed class MaxenServiceProviderFactory : IServiceProviderFactory<ContainerBuilder>
{
    private readonly Action<ContainerBuilder>? _configure;

    /// <summary>Creates the factory; <paramref name="configure"/> may add Maxen-only registrations and extensions on top of the ones from the service collection.</summary>
    public MaxenServiceProviderFactory(Action<ContainerBuilder>? configure = null) => _configure = configure;

    /// <inheritdoc/>
    public ContainerBuilder CreateBuilder(IServiceCollection services)
    {
        var builder = new ContainerBuilder();
        ServiceCollectionPopulator.Populate(builder, services);

        // Applied after the collection so Maxen-specific registrations can override or decorate it.
        _configure?.Invoke(builder);
        return builder;
    }

    /// <inheritdoc/>
    public IServiceProvider CreateServiceProvider(ContainerBuilder containerBuilder)
        => new MaxenServiceProvider(containerBuilder.Build());
}
