using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ScanningTests
{
    [Fact]
    public void Attributed_classes_and_conventions_are_registered()
    {
        var assembly = typeof(ScanningTests).Assembly;
        var builder = new ContainerBuilder();
        builder.RegisterAttributed(assembly);
        builder.RegisterAssemblyTypes(assembly, t => t == typeof(Greeter), ServiceLifetime.Singleton);
        builder.Register<IClock, Clock>();
        using var container = builder.Build();

        Assert.IsType<Scanned>(container.Resolve<IScanned>());
        Assert.Same(container.Resolve<IGreeter>(), container.Resolve<IGreeter>());
    }
}
