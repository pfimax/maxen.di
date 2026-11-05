using Microsoft.Extensions.DependencyInjection;
using MsLifetime = Microsoft.Extensions.DependencyInjection.ServiceLifetime;
using MsServiceDescriptor = Microsoft.Extensions.DependencyInjection.ServiceDescriptor;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>Translates Microsoft service descriptors into Maxen registrations.</summary>
internal static class ServiceCollectionPopulator
{
    public static void Populate(ContainerBuilder builder, IEnumerable<MsServiceDescriptor> services)
    {
        AddFrameworkServices(builder);

        foreach (var descriptor in services)
            Add(builder, descriptor);
    }

    /// <summary>Services that ASP.NET Core and the host expect every container to provide.</summary>
    private static void AddFrameworkServices(ContainerBuilder builder)
    {
        // Asking for IServiceProvider should give the Microsoft-shaped wrapper (keyed services and
        // GetRequiredService), not the bare Maxen scope. The wrapper only forwards, so the scope
        // itself stays responsible for disposal.
        builder.RegisterFactory<IServiceProvider>(ctx => new MaxenServiceProvider(ctx.Scope)).ExternallyOwned();

        builder.RegisterFactory<IServiceScopeFactory>(ctx => new MaxenServiceScopeFactory(ctx.Container)).AsSingleton();
        builder.RegisterFactory<IServiceProviderIsService>(ctx => new MaxenServiceProviderIsService(ctx.Container)).AsSingleton();
        builder.RegisterFactory<IServiceProviderIsKeyedService>(ctx => new MaxenServiceProviderIsService(ctx.Container)).AsSingleton();
    }

    private static void Add(ContainerBuilder builder, MsServiceDescriptor descriptor)
    {
        var key = descriptor.IsKeyedService ? descriptor.ServiceKey : null;

        RegistrationBuilder registration;
        if (descriptor.IsKeyedService)
        {
            registration = descriptor switch
            {
                { KeyedImplementationInstance: not null } =>
                    builder.RegisterInstance(descriptor.ServiceType, descriptor.KeyedImplementationInstance),
                { KeyedImplementationFactory: not null } =>
                    builder.RegisterFactory(descriptor.ServiceType, ctx =>
                        descriptor.KeyedImplementationFactory(new MaxenServiceProvider(ctx.Scope), key)),
                _ => builder.Register(descriptor.ServiceType, descriptor.KeyedImplementationType!),
            };
        }
        else
        {
            registration = descriptor switch
            {
                { ImplementationInstance: not null } =>
                    builder.RegisterInstance(descriptor.ServiceType, descriptor.ImplementationInstance),
                { ImplementationFactory: not null } =>
                    builder.RegisterFactory(descriptor.ServiceType, ctx =>
                        descriptor.ImplementationFactory(new MaxenServiceProvider(ctx.Scope))),
                _ => builder.Register(descriptor.ServiceType, descriptor.ImplementationType!),
            };
        }

        registration.WithKey(key);

        // A ready-made instance keeps the lifetime it was registered with (always singleton, never disposed by us).
        if (!HasInstance(descriptor))
            registration.WithLifetime(Map(descriptor.Lifetime));
    }

    private static bool HasInstance(MsServiceDescriptor descriptor)
        => descriptor.IsKeyedService
            ? descriptor.KeyedImplementationInstance is not null
            : descriptor.ImplementationInstance is not null;

    private static Lifetime Map(MsLifetime lifetime) => lifetime switch
    {
        MsLifetime.Singleton => Lifetime.Singleton,
        MsLifetime.Scoped => Lifetime.Scoped,
        MsLifetime.Transient => Lifetime.Transient,
        _ => throw new ArgumentOutOfRangeException(nameof(lifetime)),
    };
}
