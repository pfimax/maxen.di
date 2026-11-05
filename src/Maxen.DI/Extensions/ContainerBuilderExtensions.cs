using Maxen.DI.Internal;
using Maxen.DI.Pipeline;
using System.Reflection;
using Maxen.DI.Extensibility;
using Maxen.DI.Scanning;

namespace Maxen.DI;

/// <summary>Generic shortcuts and assembly scanning for <see cref="ContainerBuilder"/>.</summary>
public static class ContainerBuilderExtensions
{
    /// <summary>Registers a class as itself.</summary>
    public static RegistrationBuilder Register<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this ContainerBuilder builder)
        where TImplementation : class
        => builder.Register(typeof(TImplementation), typeof(TImplementation));

    /// <summary>Registers <typeparamref name="TImplementation"/> to be built whenever <typeparamref name="TService"/> is asked for.</summary>
    public static RegistrationBuilder Register<TService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this ContainerBuilder builder)
        where TImplementation : class, TService
        => builder.Register(typeof(TService), typeof(TImplementation));

    /// <summary>Registers a service that your own delegate builds.</summary>
    public static RegistrationBuilder RegisterFactory<TService>(this ContainerBuilder builder, Func<ResolveContext, TService> factory)
        where TService : notnull
        => builder.RegisterFactory(typeof(TService), ctx => factory(ctx));

    /// <summary>Registers an object you already have. The container will not dispose it.</summary>
    public static RegistrationBuilder RegisterInstance<TService>(this ContainerBuilder builder, TService instance)
        where TService : notnull
        => builder.RegisterInstance(typeof(TService), instance);

    /// <summary>Registers only if nothing is registered for the service yet. Returns null when it did nothing.</summary>
    public static RegistrationBuilder? TryRegister<TService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this ContainerBuilder builder)
        where TImplementation : class, TService
        => builder.IsRegistered(ServiceKey.For<TService>()) ? null : builder.Register<TService, TImplementation>();

    /// <summary>
    /// Swaps an existing registration for a new one. Unlike a plain <c>Register</c>, it fails when nothing was
    /// registered before, so a typo can't quietly leave the original in place. Earlier registrations are removed.
    /// </summary>
    public static RegistrationBuilder Replace<TService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this ContainerBuilder builder)
        where TImplementation : class, TService
    {
        RemoveExisting(builder, typeof(TService), "replace");
        return builder.Register<TService, TImplementation>();
    }

    /// <summary>Like <c>Replace</c>, but the new registration is built by your delegate.</summary>
    public static RegistrationBuilder ReplaceFactory<TService>(this ContainerBuilder builder, Func<ResolveContext, TService> factory)
        where TService : notnull
    {
        RemoveExisting(builder, typeof(TService), "replace");
        return builder.RegisterFactory(factory);
    }

    /// <summary>Like <c>Replace</c>, but the new registration is an object you already have.</summary>
    public static RegistrationBuilder ReplaceInstance<TService>(this ContainerBuilder builder, TService instance)
        where TService : notnull
    {
        RemoveExisting(builder, typeof(TService), "replace");
        return builder.RegisterInstance(instance);
    }

    /// <summary>
    /// Adds a registration that wins over the existing one but keeps it around (so <c>ResolveAll</c> still
    /// returns both). Fails when there is nothing to override.
    /// </summary>
    public static RegistrationBuilder Override<TService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this ContainerBuilder builder)
        where TImplementation : class, TService
    {
        if (!builder.IsRegistered(ServiceKey.For<TService>()))
            throw new RegistrationException(NothingToChange(typeof(TService), "override"));
        return builder.Register<TService, TImplementation>();
    }

    private static void RemoveExisting(ContainerBuilder builder, Type service, string verb)
    {
        if (builder.RemoveAll(new ServiceKey(service)) == 0)
            throw new RegistrationException(NothingToChange(service, verb));
    }

    private static string NothingToChange(Type service, string verb)
        => $"Cannot {verb} '{TypeNames.Format(service)}': nothing is registered for it yet. Use Register if you meant to add it.";

    /// <summary>Wraps every registration of <typeparamref name="TService"/> (that passes <paramref name="when"/>) in <typeparamref name="TDecorator"/>. Lower <paramref name="priority"/> sits closer to the service.</summary>
    public static ContainerBuilder Decorate<TService, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>(
        this ContainerBuilder builder, Func<ServiceDescriptor, bool>? when = null, int priority = 0)
        where TDecorator : class, TService
        => builder.Decorate(typeof(TService), typeof(TDecorator), when, priority);

    /// <summary>Adds global middleware written as a lambda.</summary>
    public static ContainerBuilder Use(this ContainerBuilder builder, MiddlewarePhase phase, Func<ResolveContext, ResolveDelegate, object> middleware)
        => builder.Use(phase, new DelegateMiddleware(middleware));

    /// <summary>Creates a <typeparamref name="TModule"/> and applies it to the builder.</summary>
    public static ContainerBuilder RegisterModule<TModule>(this ContainerBuilder builder)
        where TModule : IContainerModule, new()
        => builder.RegisterModule(new TModule());

    /// <summary>
    /// Applies a module only when the named feature is switched on (see <see cref="UseFeatures"/>). A feature is a
    /// profile under another name, so a switched-on feature wins over the defaults the same way a profile does,
    /// and the same module can be listed under several features.
    /// </summary>
    public static ContainerBuilder RegisterModule(this ContainerBuilder builder, string feature, IContainerModule module)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(module);
        return builder.DefineProfile(feature, b => b.RegisterModule(module));
    }

    /// <summary>Creates a <typeparamref name="TModule"/> and applies it only when the named feature is switched on.</summary>
    public static ContainerBuilder RegisterModule<TModule>(this ContainerBuilder builder, string feature)
        where TModule : IContainerModule, new()
        => builder.RegisterModule(feature, new TModule());

    /// <summary>Switches on features, so the modules and profiles registered under those names are applied at build time.</summary>
    public static ContainerBuilder UseFeatures(this ContainerBuilder builder, params string[] features)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(features);

        foreach (var feature in features) builder.UseProfile(feature);
        return builder;
    }

    /// <summary>
    /// Switches on the features listed in an environment variable (MAXEN_FEATURES unless you say otherwise), separated
    /// by commas or semicolons. Does nothing if the variable is not set, so a flag can be flipped without a rebuild.
    /// </summary>
    public static ContainerBuilder UseFeaturesFromEnvironment(this ContainerBuilder builder, string variable = "MAXEN_FEATURES")
    {
        ArgumentNullException.ThrowIfNull(builder);

        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value)) return builder;

        var names = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return builder.UseFeatures(names);
    }

    /// <summary>Turns on <see cref="InjectAttribute"/> for public properties. Off by default to keep constructors the main way in.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Property injection reads the properties of each created object by reflection.")]
    public static ContainerBuilder UsePropertyInjection(this ContainerBuilder builder)
        => builder.AddInitializer(new PropertyInjectionInitializer());

    /// <summary>Turns on <see cref="InjectAttribute"/> for public methods, which are called after construction.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Method injection reads the methods of each created object by reflection.")]
    public static ContainerBuilder UseMethodInjection(this ContainerBuilder builder)
        => builder.AddInitializer(new MethodInjectionInitializer());

    /// <summary>Registers every class in the assembly that carries a <see cref="ServiceAttribute"/>, and applies every <see cref="DecoratorAttribute"/>.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Scanning reads types by reflection, so types nothing else references may be trimmed away. Register services explicitly or use the source generator when trimming.")]
    public static ContainerBuilder RegisterAttributed(this ContainerBuilder builder, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract) continue;

            // A class can be a decorator without being a service of its own.
            var decorator = type.GetCustomAttribute<DecoratorAttribute>(false);
            if (decorator is not null) builder.Decorate(decorator.ServiceType, type, null, decorator.Priority);

            var attribute = type.GetCustomAttribute<ServiceAttribute>(false);
            if (attribute is null) continue;

            var registration = builder.Register(attribute.ServiceType ?? type, type)
                .WithLifetime(Lifetime.From(attribute.Lifetime))
                .WithKey(attribute.Key);

            // OrderBy is stable, so interceptors with the same Order keep the order the attributes were read in.
            foreach (var intercept in type.GetCustomAttributes<InterceptAttribute>(false).OrderBy(a => a.Order))
            {
                var interceptor = Activator.CreateInstance(intercept.InterceptorType) as Interception.IInterceptor
                    ?? throw new RegistrationException(
                        $"'{TypeNames.Format(intercept.InterceptorType)}' on '{TypeNames.Format(type)}' is not an IInterceptor with a public parameterless constructor.");
                registration = Interception.InterceptionExtensions.InterceptWith(registration, interceptor);
            }
        }
        return builder;
    }

    /// <summary>Finds every module in the assembly that has a public parameterless constructor and applies it.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Scanning reads types by reflection, so types nothing else references may be trimmed away. Register services explicitly or use the source generator when trimming.")]
    public static ContainerBuilder RegisterModules(this ContainerBuilder builder, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || !typeof(IContainerModule).IsAssignableFrom(type)) continue;
            if (type.GetConstructor(Type.EmptyTypes) is null) continue;

            builder.RegisterModule((IContainerModule)Activator.CreateInstance(type)!);
        }
        return builder;
    }

    /// <summary>
    /// Convention-based registration. Every concrete class that passes <paramref name="filter"/> is
    /// registered as the types <paramref name="serviceTypes"/> returns; by default that's its public
    /// interfaces, or the class itself if it has none.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Scanning reads types by reflection, so types nothing else references may be trimmed away. Register services explicitly or use the source generator when trimming.")]
    public static ContainerBuilder RegisterAssemblyTypes(
        this ContainerBuilder builder,
        Assembly assembly,
        Func<Type, bool> filter,
        ServiceLifetime lifetime = ServiceLifetime.Transient,
        Func<Type, IEnumerable<Type>>? serviceTypes = null)
    {
        // Kept for convenience; the fluent Scan API does the actual work.
        return builder.Scan(assembly, scan =>
        {
            scan.Where(filter).WithLifetime(Lifetime.From(lifetime));

            if (serviceTypes is not null)
            {
                scan.As(serviceTypes);
                return;
            }

            // Default: public interfaces, or the class itself when it has none.
            scan.As(type =>
            {
                var interfaces = type.GetInterfaces()
                    .Where(i => i.IsPublic && i.Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
                    .ToList();
                return interfaces.Count > 0 ? interfaces : new[] { type };
            });
        });
    }
}
