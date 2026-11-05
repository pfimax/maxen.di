using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class KeyedTests
{
    [Fact]
    public void Keyed_services_are_resolved_by_key_or_attribute()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().WithKey("slow");
            b.RegisterInstance<IClock>(new Clock()).WithKey("fast");
            b.Register<NeedsFastClock>();
        });

        Assert.Same(container.Resolve<NeedsFastClock>().Clock, container.Resolve<IClock>("fast"));
        Assert.NotSame(container.Resolve<IClock>("slow"), container.Resolve<IClock>("slow"));
    }

    [Fact]
    public void A_keyed_registration_does_not_answer_for_the_unkeyed_service()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>().WithKey("slow"));

        Assert.False(container.IsRegistered<IClock>());
    }

    [Fact]
    public void AlsoAs_shares_one_singleton_across_service_types()
    {
        using var container = TestContainer.Build(b => b.Register<Clock>().AsSingleton().AlsoAs<IClock>());

        Assert.Same(container.Resolve<Clock>(), container.Resolve<IClock>());
    }
}
