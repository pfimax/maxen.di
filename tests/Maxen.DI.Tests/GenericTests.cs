using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class GenericTests
{
    [Fact]
    public void Open_generics_are_closed_on_demand()
    {
        using var container = TestContainer.Build(b => b.Register(typeof(IRepo<>), typeof(Repo<>)).AsSingleton());

        var repo = container.Resolve<IRepo<string>>();

        Assert.IsType<Repo<string>>(repo);
        Assert.Same(repo, container.Resolve<IRepo<string>>());
        Assert.NotSame(repo, container.Resolve<IRepo<int>>());
    }

    [Fact]
    public void Closed_registrations_join_the_open_one_in_registration_order()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register(typeof(IRepo<>), typeof(Repo<>));
            b.Register<IRepo<int>, CachedRepo<int>>();
            b.RegisterInstance<IRepo<int>>(new Repo<int>());
        });

        Assert.Equal(3, container.ResolveAll<IRepo<int>>().Count);
    }

    [Fact]
    public void Open_generic_decorators_wrap_every_closed_form()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register(typeof(IRepo<>), typeof(Repo<>));
            b.Decorate(typeof(IRepo<>), typeof(CachedRepo<>));
        });

        var decorated = Assert.IsType<CachedRepo<string>>(container.Resolve<IRepo<string>>());

        Assert.IsType<Repo<string>>(decorated.Inner);
    }
}
