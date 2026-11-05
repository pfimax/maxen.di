using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class MethodInjectionTests
{
    [Fact]
    public void Marked_methods_are_called_with_their_services_after_construction()
    {
        using var container = TestContainer.Build(b =>
        {
            b.UseMethodInjection();
            b.Register<IClock, Clock>();
            b.Register<MethodInjected>();
        });

        var instance = container.Resolve<MethodInjected>();

        Assert.NotNull(instance.Clock);
        Assert.Equal(1, instance.Calls);
    }

    [Fact]
    public void Methods_are_ignored_unless_method_injection_is_switched_on()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<MethodInjected>();
        });

        Assert.Null(container.Resolve<MethodInjected>().Clock);
    }

    [Fact]
    public void A_missing_service_for_the_method_is_reported_with_its_path()
    {
        using var container = TestContainer.Build(b =>
        {
            b.UseMethodInjection();
            b.Register<MethodInjected>();
        });

        var error = Assert.Throws<ResolutionException>(() => container.Resolve<MethodInjected>());
        Assert.Contains("IClock", error.Message);
    }
}
