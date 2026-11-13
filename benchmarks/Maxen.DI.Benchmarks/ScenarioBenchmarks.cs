using Autofac;
using BenchmarkDotNet.Attributes;
using Maxen.DI.Benchmarks.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Benchmarks;

/// <summary>
/// Scenarios beyond the plain resolve: starting up, graphs whose services are shared by many others, validation,
/// decorators, collections and keyed services. The Microsoft and Autofac rows are there where those containers have the
/// same feature.
/// </summary>
[MemoryDiagnoser]
public class ScenarioBenchmarks
{
    // Deep enough for sharing to matter (two paths per level: 4096 ways to the bottom), small enough to run quickly.
    private const int Depth = 12;

    private Container _maxen = null!;
    private Container _decorated = null!;
    private Container _shared = null!;
    private ServiceProvider _microsoft = null!;
    private Autofac.IContainer _autofac = null!;
    private Type _sharedTop = null!;

    [GlobalSetup]
    public void Setup()
    {
        var maxen = new Maxen.DI.ContainerBuilder();
        Register(maxen);
        _maxen = maxen.Build();

        var decorated = new Maxen.DI.ContainerBuilder();
        Register(decorated);
        decorated.Decorate<IBenchService, TimedBenchService>();
        _decorated = decorated.Build();

        _shared = BuildShared(out _sharedTop);

        var services = new ServiceCollection();
        Register(services);
        _microsoft = services.BuildServiceProvider();

        var autofac = new Autofac.ContainerBuilder();
        Register(autofac);
        _autofac = autofac.Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _maxen.Dispose();
        _decorated.Dispose();
        _shared.Dispose();
        _microsoft.Dispose();
        _autofac.Dispose();
    }

    private static void Register(Maxen.DI.ContainerBuilder builder)
    {
        builder.Register<IBenchLogger, BenchLogger>().AsSingleton();
        builder.Register<IBenchLogger, BenchLogger>().AsSingleton();
        builder.Register<IBenchLogger, BenchLogger>().AsSingleton();
        builder.Register<IBenchLogger, BenchLogger>().AsSingleton().WithKey("audit");
        builder.Register<IBenchRepository, BenchRepository>().AsScoped();
        builder.Register<IBenchService, BenchService>();
    }

    private static void Register(IServiceCollection services)
    {
        services.AddSingleton<IBenchLogger, BenchLogger>();
        services.AddSingleton<IBenchLogger, BenchLogger>();
        services.AddSingleton<IBenchLogger, BenchLogger>();
        services.AddKeyedSingleton<IBenchLogger, BenchLogger>("audit");
        services.AddScoped<IBenchRepository, BenchRepository>();
        services.AddTransient<IBenchService, BenchService>();
    }

    private static void Register(Autofac.ContainerBuilder builder)
    {
        builder.RegisterType<BenchLogger>().As<IBenchLogger>().SingleInstance();
        builder.RegisterType<BenchLogger>().As<IBenchLogger>().SingleInstance();
        builder.RegisterType<BenchLogger>().As<IBenchLogger>().SingleInstance();
        builder.RegisterType<BenchLogger>().Keyed<IBenchLogger>("audit").SingleInstance();
        builder.RegisterType<BenchRepository>().As<IBenchRepository>().InstancePerLifetimeScope();
        builder.RegisterType<BenchService>().As<IBenchService>().InstancePerDependency();
    }

    // BenchPair<BenchPair<...BenchLeaf>>, every level registered as a singleton of its own.
    private static Container BuildShared(out Type top)
    {
        var builder = new Maxen.DI.ContainerBuilder();
        builder.Register<BenchLeaf>().AsSingleton();

        top = typeof(BenchLeaf);
        for (var i = 0; i < Depth; i++)
        {
            top = typeof(BenchPair<>).MakeGenericType(top);
            builder.Register(top, top).AsSingleton();
        }

        return builder.Build();
    }

    // ---- Starting up: build the container, serve the first request, shut down ------------------

    [Benchmark(Description = "Startup: Maxen.DI")]
    public object StartupMaxen()
    {
        var builder = new Maxen.DI.ContainerBuilder();
        Register(builder);
        using var container = builder.Build();
        using var scope = container.CreateScope();
        return scope.Resolve<IBenchService>();
    }

    [Benchmark(Description = "Startup: Microsoft")]
    public object StartupMicrosoft()
    {
        var services = new ServiceCollection();
        Register(services);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IBenchService>();
    }

    [Benchmark(Description = "Startup: Autofac")]
    public object StartupAutofac()
    {
        var builder = new Autofac.ContainerBuilder();
        Register(builder);
        using var container = builder.Build();
        using var scope = container.BeginLifetimeScope();
        return scope.Resolve<IBenchService>();
    }

    // ---- A graph of shared services: the first resolve, and checking it -------------------------

    [Benchmark(Description = "Shared graph, first resolve: Maxen.DI")]
    public object SharedFirstResolveMaxen()
    {
        using var container = BuildShared(out var top);
        return container.Resolve(top);
    }

    [Benchmark(Description = "Shared graph, validate: Maxen.DI")]
    public object SharedValidateMaxen() => _shared.Validate();

    // ---- A decorated service, which takes the full pipeline ------------------------------------

    [Benchmark(Description = "Decorated, scope + graph: Maxen.DI")]
    public object DecoratedMaxen()
    {
        using var scope = _decorated.CreateScope();
        return scope.Resolve<IBenchService>();
    }

    // ---- Every registration of a service --------------------------------------------------------

    [Benchmark(Description = "Collection of 3: Maxen.DI")]
    public object CollectionMaxen() => _maxen.Resolve<IEnumerable<IBenchLogger>>();

    [Benchmark(Description = "Collection of 3: Microsoft")]
    public object CollectionMicrosoft() => _microsoft.GetRequiredService<IEnumerable<IBenchLogger>>();

    [Benchmark(Description = "Collection of 3: Autofac")]
    public object CollectionAutofac() => _autofac.Resolve<IEnumerable<IBenchLogger>>();

    // ---- A keyed singleton ----------------------------------------------------------------------

    [Benchmark(Description = "Keyed singleton: Maxen.DI")]
    public object KeyedMaxen() => _maxen.Resolve<IBenchLogger>("audit");

    [Benchmark(Description = "Keyed singleton: Microsoft")]
    public object KeyedMicrosoft() => _microsoft.GetRequiredKeyedService<IBenchLogger>("audit");

    [Benchmark(Description = "Keyed singleton: Autofac")]
    public object KeyedAutofac() => _autofac.ResolveKeyed<IBenchLogger>("audit");
}
