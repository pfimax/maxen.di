using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>Use Maxen.DI without the Generic Host.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Copies the registrations of a Microsoft service collection into a Maxen builder.</summary>
    public static ContainerBuilder Populate(this ContainerBuilder builder, IServiceCollection services)
    {
        ServiceCollectionPopulator.Populate(builder, services);
        return builder;
    }

    /// <summary>
    /// Builds a service provider from the collection using Maxen.DI. Dispose the result when you are done
    /// with it; that also disposes the singletons the container created.
    /// </summary>
    public static IServiceProvider BuildMaxenServiceProvider(
        this IServiceCollection services, Action<ContainerBuilder>? configure = null)
    {
        var factory = new MaxenServiceProviderFactory(configure);
        return factory.CreateServiceProvider(factory.CreateBuilder(services));
    }
}
