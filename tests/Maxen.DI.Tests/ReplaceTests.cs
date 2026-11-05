using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ReplaceTests
{
    [Fact]
    public void Replace_removes_the_original_registration()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
            b.Replace<IGreeter, QuietGreeter>();
        });

        Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
        Assert.Single(container.ResolveAll<IGreeter>());
    }

    [Fact]
    public void Override_wins_but_keeps_the_original_for_ResolveAll()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
            b.Override<IGreeter, QuietGreeter>();
        });

        Assert.IsType<QuietGreeter>(container.Resolve<IGreeter>());
        Assert.Equal(2, container.ResolveAll<IGreeter>().Count);
    }

    [Fact]
    public void Replacing_something_that_was_never_registered_fails()
    {
        var builder = new ContainerBuilder();

        var error = Assert.Throws<RegistrationException>(() => builder.Replace<IGreeter, QuietGreeter>());
        Assert.Contains("IGreeter", error.Message);
    }

    [Fact]
    public void Overriding_something_that_was_never_registered_fails()
    {
        var builder = new ContainerBuilder();

        Assert.Throws<RegistrationException>(() => builder.Override<IGreeter, QuietGreeter>());
    }

    [Fact]
    public void ReplaceInstance_swaps_in_a_ready_made_object()
    {
        var replacement = new QuietGreeter();
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
            b.ReplaceInstance<IGreeter>(replacement);
        });

        Assert.Same(replacement, container.Resolve<IGreeter>());
    }
}
