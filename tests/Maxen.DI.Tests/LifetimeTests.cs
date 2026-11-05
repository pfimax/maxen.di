using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class LifetimeTests
{
    [Fact]
    public void Resolves_constructor_dependencies()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });

        var greeter = (Greeter)container.Resolve<IGreeter>();

        Assert.NotNull(greeter.Clock);
    }

    [Fact]
    public void Singleton_scoped_and_transient_behave_as_advertised()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<Clock>().AsSingleton();
            b.Register<Disposable>().AsScoped();
            b.Register<IClock, Clock>();
            b.Register<Greeter>();
        });

        Assert.Same(container.Resolve<Clock>(), container.Resolve<Clock>());

        using var first = container.CreateScope();
        using var second = container.CreateScope();
        Assert.Same(first.Resolve<Disposable>(), first.Resolve<Disposable>());
        Assert.NotSame(first.Resolve<Disposable>(), second.Resolve<Disposable>());

        Assert.NotSame(container.Resolve<Greeter>(), container.Resolve<Greeter>());
    }

    [Fact]
    public void Singleton_is_created_once_even_under_contention()
    {
        using var container = TestContainer.Build(b => b.Register<Clock>().AsSingleton());
        var seen = new System.Collections.Concurrent.ConcurrentBag<Clock>();

        Parallel.For(0, 200, _ => seen.Add(container.Resolve<Clock>()));

        Assert.Single(seen.Distinct());
    }

    [Fact]
    public void Container_and_scope_can_be_injected()
    {
        using var container = TestContainer.Build(_ => { });
        using var scope = container.CreateScope();

        Assert.Same(container, container.Resolve<IContainer>());
        Assert.Same(scope, scope.Resolve<IScope>());
    }

    [Fact]
    public void A_custom_lifetime_plugs_in()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<PerThreadClockModule>();
        using var container = builder.Build();

        var mine = container.Resolve<Clock>();
        Assert.Same(mine, container.Resolve<Clock>());

        // A real thread, not Task.Run: tasks waited on can be inlined onto the calling thread.
        Clock? theirs = null;
        var thread = new Thread(() => theirs = container.Resolve<Clock>());
        thread.Start();
        thread.Join();

        Assert.NotNull(theirs);
        Assert.NotSame(mine, theirs);
    }
}
