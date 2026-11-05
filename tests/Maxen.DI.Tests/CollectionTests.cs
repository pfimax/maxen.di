using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class CollectionTests
{
    [Fact]
    public void Collections_gather_every_registration()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
            b.Register<IGreeter, QuietGreeter>();
            b.Register<GreeterCollection>();
        });

        Assert.Equal(2, container.Resolve<GreeterCollection>().All.Count);
        Assert.Equal(2, container.Resolve<IGreeter[]>().Length);
    }

    [Fact]
    public void Asking_for_a_collection_of_nothing_gives_an_empty_one()
    {
        using var container = TestContainer.Build(_ => { });

        Assert.Empty(container.Resolve<IEnumerable<Disposable>>());
    }

    [Fact]
    public void Lazy_defers_creation_until_first_use()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>());

        var lazy = container.Resolve<Lazy<IClock>>();

        Assert.False(lazy.IsValueCreated);
        Assert.NotNull(lazy.Value);
    }

    [Fact]
    public void Func_creates_a_fresh_transient_each_call()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>());

        var factory = container.Resolve<Func<IClock>>();

        Assert.NotSame(factory(), factory());
    }

    [Fact]
    public void Lazy_of_an_unregistered_service_is_not_available()
    {
        using var container = TestContainer.Build(_ => { });

        Assert.False(container.IsRegistered<Lazy<IClock>>());
    }
}
