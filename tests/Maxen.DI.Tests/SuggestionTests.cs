using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class SuggestionTests
{
    [Fact]
    public void Asking_for_the_class_suggests_the_interface_it_was_registered_under()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });

        var error = Assert.Throws<ServiceNotRegisteredException>(() => container.Resolve<Greeter>());

        Assert.Contains("IGreeter", error.Message);
        Assert.NotEmpty(error.Suggestions);
    }

    [Fact]
    public void Forgetting_the_key_suggests_the_keyed_registration()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>().WithKey("plain");
        });

        var error = Assert.Throws<ServiceNotRegisteredException>(() => container.Resolve<IGreeter>());

        Assert.Contains("plain", error.Message);
    }

    [Fact]
    public void Suggestions_include_what_the_child_container_registered()
    {
        using var parent = TestContainer.Build(b => b.Register<IClock, Clock>());
        using var child = parent.CreateChild(b => b.Register<IGreeter, Greeter>().WithKey("plain"));

        var error = Assert.Throws<ServiceNotRegisteredException>(() => child.Resolve<IGreeter>());

        Assert.Contains("plain", error.Message);
    }

    [Fact]
    public void Nothing_similar_means_no_suggestions()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>());

        var error = Assert.Throws<ServiceNotRegisteredException>(() => container.Resolve<IGreeter>());

        Assert.Empty(error.Suggestions);
        Assert.DoesNotContain("Did you mean", error.Message);
    }
}
