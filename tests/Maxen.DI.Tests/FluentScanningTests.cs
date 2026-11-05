using Maxen.DI.Scanning;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class FluentScanningTests
{
    private static readonly System.Reflection.Assembly TestAssembly = typeof(FluentScanningTests).Assembly;

    [Fact]
    public void Classes_are_picked_by_filter_and_registered_as_their_interfaces()
    {
        var builder = new ContainerBuilder();
        builder.Register<IClock, Clock>();
        builder.Scan(TestAssembly, s => s
            .InNamespace("Maxen.DI.Tests.Fixtures")
            .AssignableTo<IGreeter>()
            .Where(t => t == typeof(Greeter))
            .AsImplementedInterfaces()
            .AsSingleton());
        using var container = builder.Build();

        Assert.Same(container.Resolve<IGreeter>(), container.Resolve<IGreeter>());
    }

    [Fact]
    public void AsSelf_and_interfaces_share_one_singleton()
    {
        var builder = new ContainerBuilder();
        builder.Scan(TestAssembly, s => s
            .Where(t => t == typeof(Clock))
            .AsSelf()
            .AsImplementedInterfaces()
            .AsSingleton());
        using var container = builder.Build();

        Assert.Same(container.Resolve<Clock>(), container.Resolve<IClock>());
    }

    [Fact]
    public void Open_generic_classes_are_registered_as_open_generics()
    {
        var builder = new ContainerBuilder();
        builder.Scan(TestAssembly, s => s
            .Where(t => t == typeof(Repo<>))
            .AsImplementedInterfaces());
        using var container = builder.Build();

        Assert.IsType<Repo<int>>(container.Resolve<IRepo<int>>());
    }

    [Fact]
    public void Abstract_and_generated_types_are_ignored()
    {
        var builder = new ContainerBuilder();
        builder.Scan(TestAssembly, s => s.InNamespace("Maxen.DI.Tests.Fixtures").AsSelf());
        using var container = builder.Build();

        Assert.True(container.IsRegistered<Clock>());
    }
}
