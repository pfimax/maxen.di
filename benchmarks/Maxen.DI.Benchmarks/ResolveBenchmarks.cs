using Autofac;
using BenchmarkDotNet.Attributes;
using Maxen.DI.Benchmarks.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Benchmarks;

/// <summary>
/// Compares resolving the same small graph (service -> repository -> logger) across containers.
/// Each container is set up with identical lifetimes: logger singleton, repository scoped, service transient.
/// </summary>
[MemoryDiagnoser]
public class ResolveBenchmarks
{
    private Container _maxen = null!;
    private ServiceProvider _microsoft = null!;
    private Autofac.IContainer _autofac = null!;

    [GlobalSetup]
    public void Setup()
    {
        var maxen = new Maxen.DI.ContainerBuilder();
        maxen.Register<IBenchLogger, BenchLogger>().AsSingleton();
        maxen.Register<IBenchRepository, BenchRepository>().AsScoped();
        maxen.Register<IBenchService, BenchService>();
        _maxen = maxen.Build();

        var services = new ServiceCollection();
        services.AddSingleton<IBenchLogger, BenchLogger>();
        services.AddScoped<IBenchRepository, BenchRepository>();
        services.AddTransient<IBenchService, BenchService>();
        _microsoft = services.BuildServiceProvider();

        var autofac = new Autofac.ContainerBuilder();
        autofac.RegisterType<BenchLogger>().As<IBenchLogger>().SingleInstance();
        autofac.RegisterType<BenchRepository>().As<IBenchRepository>().InstancePerLifetimeScope();
        autofac.RegisterType<BenchService>().As<IBenchService>().InstancePerDependency();
        _autofac = autofac.Build();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _maxen.Dispose();
        _microsoft.Dispose();
        _autofac.Dispose();
    }

    // ---- A singleton looked up repeatedly ----------------------------------------------------

    [Benchmark(Description = "Singleton: Maxen.DI")]
    public object SingletonMaxen() => _maxen.Resolve<IBenchLogger>();

    [Benchmark(Description = "Singleton: Microsoft")]
    public object SingletonMicrosoft() => _microsoft.GetRequiredService<IBenchLogger>();

    [Benchmark(Description = "Singleton: Autofac")]
    public object SingletonAutofac() => _autofac.Resolve<IBenchLogger>();

    // ---- A typical request: open a scope, resolve a three-level graph, close the scope --------

    [Benchmark(Description = "Scope + graph: Maxen.DI")]
    public object ScopeGraphMaxen()
    {
        using var scope = _maxen.CreateScope();
        return scope.Resolve<IBenchService>();
    }

    [Benchmark(Description = "Scope + graph: Microsoft")]
    public object ScopeGraphMicrosoft()
    {
        using var scope = _microsoft.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IBenchService>();
    }

    [Benchmark(Description = "Scope + graph: Autofac")]
    public object ScopeGraphAutofac()
    {
        using var scope = _autofac.BeginLifetimeScope();
        return scope.Resolve<IBenchService>();
    }

    // ---- The same scoped service asked for again and again inside one scope (a request touching many consumers) ----

    [Benchmark(Description = "Scoped x10: Maxen.DI")]
    public object ScopedRepeatMaxen()
    {
        using var scope = _maxen.CreateScope();
        object? last = null;
        for (var i = 0; i < 10; i++) last = scope.Resolve<IBenchRepository>();
        return last!;
    }

    [Benchmark(Description = "Scoped x10: Microsoft")]
    public object ScopedRepeatMicrosoft()
    {
        using var scope = _microsoft.CreateScope();
        object? last = null;
        for (var i = 0; i < 10; i++) last = scope.ServiceProvider.GetRequiredService<IBenchRepository>();
        return last!;
    }

    [Benchmark(Description = "Scoped x10: Autofac")]
    public object ScopedRepeatAutofac()
    {
        using var scope = _autofac.BeginLifetimeScope();
        object? last = null;
        for (var i = 0; i < 10; i++) last = scope.Resolve<IBenchRepository>();
        return last!;
    }
}
