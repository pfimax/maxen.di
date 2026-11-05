using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class DecoratorTests
{
    [Fact]
    public void Decorator_wraps_the_service_and_keeps_its_lifetime()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>().AsSingleton();
            b.Decorate<IGreeter, LoudGreeter>();
        });

        var greeter = container.Resolve<IGreeter>();

        Assert.IsType<LoudGreeter>(greeter);
        Assert.Equal("HELLO", greeter.Greet());
        Assert.Same(greeter, container.Resolve<IGreeter>());
    }

    [Fact]
    public void Equal_priorities_keep_registration_order()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
            b.Decorate<IGreeter, ExcitedGreeter>();
            b.Decorate<IGreeter, LoudGreeter>();
        });

        // Excited is closest to the service, Loud wraps it: "hello!" then upper-cased.
        Assert.Equal("HELLO!", container.Resolve<IGreeter>().Greet());
        Assert.IsType<LoudGreeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void A_lower_priority_sits_closer_to_the_service_regardless_of_registration_order()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();

            // Registered first, but told to sit on the outside.
            b.Decorate<IGreeter, ExcitedGreeter>(priority: 10);
            b.Decorate<IGreeter, LoudGreeter>(priority: 0);
        });

        // Loud is innermost ("HELLO"), Excited is outermost ("HELLO!").
        Assert.Equal("HELLO!", container.Resolve<IGreeter>().Greet());
        Assert.IsType<ExcitedGreeter>(container.Resolve<IGreeter>());
    }

    [Fact]
    public void Decorator_can_be_limited_with_a_predicate()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>().WithKey("plain");
            b.Register<IGreeter, QuietGreeter>().WithKey("quiet");
            b.Decorate(typeof(IGreeter), typeof(LoudGreeter), d => Equals(d.Key.Key, "quiet"));
        });

        Assert.IsType<Greeter>(container.Resolve<IGreeter>("plain"));
        Assert.IsType<LoudGreeter>(container.Resolve<IGreeter>("quiet"));
    }
}
