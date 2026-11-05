using Maxen.DI.Pipeline;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ConditionalRegistrationTests
{
    [Fact]
    public void A_registration_can_be_limited_to_one_consumer()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>();
            b.Register<IGreeter, Greeter>();
        });

        // Greeter gets the special clock; everyone else (here: a direct request) gets the normal one.
        Assert.IsType<FrozenClock>(((Greeter)container.Resolve<IGreeter>()).Clock);
        Assert.IsType<Clock>(container.Resolve<IClock>());
    }

    [Fact]
    public void Other_consumers_fall_back_to_the_previous_registration()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>();
            b.Register<NeedsClock>();
        });

        Assert.IsType<Clock>(container.Resolve<NeedsClock>().Clock);
    }

    [Fact]
    public void A_custom_condition_sees_the_asking_service()
    {
        ResolveContext? seen = null;
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().When(asker =>
            {
                seen = asker;
                return true;
            });
            b.Register<IGreeter, Greeter>();
        });

        container.Resolve<IGreeter>();

        Assert.NotNull(seen);
        Assert.Equal(typeof(IGreeter), seen!.Key.Type);
    }

    [Fact]
    public void With_no_applicable_registration_the_service_counts_as_missing()
    {
        using var container = TestContainer.Build(b =>
            b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>());

        Assert.Throws<ServiceNotRegisteredException>(() => container.Resolve<IClock>());
    }

    [Fact]
    public void ResolveAll_leaves_out_registrations_that_decline()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>();
        });

        Assert.Single(container.ResolveAll<IClock>());
    }

    [Fact]
    public void A_collection_parameter_only_gets_registrations_that_accept_the_consumer()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().WhenInjectedInto<ClockCollection>();
            b.Register<IClock, Clock>().WhenInjectedInto<Greeter>();
            b.Register<ClockCollection>();
        });

        var all = container.Resolve<ClockCollection>().All;

        // The Greeter-only clock declines; the open one and the ClockCollection-only one both take part.
        Assert.Equal(2, all.Count);
        Assert.Contains(all, c => c is FrozenClock);
    }
}
