using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class SourceGeneratorTests
{
    private static Container Build()
    {
        var builder = new ContainerBuilder();
        builder.Register<IClock, Clock>();
        builder.RegisterInstance<IClock>(new Clock()).WithKey("fast");
        builder.Register<IGreeter, QuietGreeter>();

        // Generated at compile time from the [Service] attributes in this assembly.
        builder.RegisterGeneratedServices();
        return builder.Build();
    }

    [Fact]
    public void Generated_registrations_honour_lifetime_and_dependencies()
    {
        using var container = Build();

        var thing = container.Resolve<IGeneratedThing>();

        Assert.Equal("generated with 1 greeter(s)", thing.Describe());
        Assert.Same(thing, container.Resolve<IGeneratedThing>()); // singleton
    }

    [Fact]
    public void Generated_registrations_support_keys_and_keyed_parameters()
    {
        using var container = Build();

        var special = (KeyedGeneratedThing)container.Resolve<IGeneratedThing>("special");

        Assert.Equal("special", special.Describe());
        Assert.Same(container.Resolve<IClock>("fast"), special.Clock);
    }

    [Fact]
    public void Generated_registrations_are_picked_up_without_reflection_scanning()
    {
        using var container = Build();

        // Scanned only has the [Service] attribute; nothing here scans the assembly at runtime.
        Assert.IsType<Scanned>(container.Resolve<IScanned>());
    }

    [Fact]
    public void Decorator_attributes_are_applied_by_the_generated_registrations_in_priority_order()
    {
        var builder = new ContainerBuilder();
        builder.RegisterGeneratedServices();
        using var container = builder.Build();

        // Priority 0 sits next to the service, priority 5 wraps it from outside.
        Assert.Equal(">> hi!", container.Resolve<IShouter>().Shout("hi"));
    }

    [Fact]
    public void Decorator_attributes_are_applied_by_the_reflection_scan_too()
    {
        var builder = new ContainerBuilder();
        builder.RegisterAttributed(typeof(SourceGeneratorTests).Assembly);
        using var container = builder.Build();

        Assert.Equal(">> hi!", container.Resolve<IShouter>().Shout("hi"));
    }

    [Fact]
    public void Intercept_attributes_wrap_the_service_in_a_generated_proxy_and_honour_order()
    {
        var builder = new ContainerBuilder();
        builder.RegisterGeneratedServices();
        using var container = builder.Build();

        var tally = container.Resolve<ITallyService>();

        // Written at compile time, not by the runtime proxy generator.
        Assert.StartsWith("GeneratedProxy", tally.GetType().Name);

        // Order 0 (AddTen) is outermost, Order 1 (TimesTwo) is inside: (2 + 3) * 2 + 10.
        Assert.Equal(20, tally.Add(2, 3));
    }

    [Fact]
    public void Intercept_attributes_work_through_the_reflection_scan_too()
    {
        var builder = new ContainerBuilder();
        builder.RegisterAttributed(typeof(SourceGeneratorTests).Assembly);
        using var container = builder.Build();

        Assert.Equal(20, container.Resolve<ITallyService>().Add(2, 3));
    }

    [Fact]
    public void Open_generic_decorators_declared_by_attribute_wrap_every_closed_form()
    {
        var generated = new ContainerBuilder();
        generated.Register(typeof(IShelf<>), typeof(Shelf<>));
        generated.RegisterGeneratedServices();
        using var viaGenerator = generated.Build();

        var scanned = new ContainerBuilder();
        scanned.Register(typeof(IShelf<>), typeof(Shelf<>));
        scanned.RegisterAttributed(typeof(SourceGeneratorTests).Assembly);
        using var viaScan = scanned.Build();

        Assert.Equal("[Int32]", viaGenerator.Resolve<IShelf<int>>().Label());
        Assert.Equal("[String]", viaGenerator.Resolve<IShelf<string>>().Label());
        Assert.Equal("[Int32]", viaScan.Resolve<IShelf<int>>().Label());
    }
}
