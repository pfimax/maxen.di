using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ScopeLifecycleTests
{
    // Builds a container whose scopes dispose their children, which is the opt-in behaviour under test.
    private static Container BuildDisposingChildren(Action<ContainerBuilder> configure)
    {
        var builder = new ContainerBuilder();
        builder.Options.DisposeChildScopes = true;
        configure(builder);
        return builder.Build();
    }

    [Fact]
    public void A_scoped_service_is_reused_inside_its_scope_and_not_across_scopes()
    {
        using var container = TestContainer.Build(b => b.Register<Clock>().AsScoped());

        using var first = container.CreateScope();
        using var second = container.CreateScope();

        // The second resolve takes the shortcut; it has to hand back the very same object.
        Assert.Same(first.Resolve<Clock>(), first.Resolve<Clock>());
        Assert.NotSame(first.Resolve<Clock>(), second.Resolve<Clock>());
    }

    [Fact]
    public void The_scoped_shortcut_does_not_let_the_root_resolve_scoped_services()
    {
        using var container = TestContainer.Build(b => b.Register<Clock>().AsScoped());
        using (var scope = container.CreateScope()) scope.Resolve<Clock>();

        Assert.Throws<ScopeValidationException>(() => container.Resolve<Clock>());
    }

    [Fact]
    public void The_scoped_shortcut_still_stops_a_singleton_from_capturing_it()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsScoped();
            b.Register<NeedsClock>().AsSingleton();
        });
        using var scope = container.CreateScope();
        scope.Resolve<IClock>(); // now cached in the scope, which is the tempting shortcut case

        Assert.Throws<ScopeValidationException>(() => scope.Resolve<NeedsClock>());
    }

    [Fact]
    public void Resolving_from_a_disposed_scope_throws_even_when_the_service_was_cached()
    {
        using var container = TestContainer.Build(b => b.Register<Clock>().AsScoped());
        var scope = container.CreateScope();
        scope.Resolve<Clock>();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Resolve<Clock>());
    }

    [Fact]
    public async Task Disposing_a_scope_while_others_resolve_never_crashes_in_a_surprising_way()
    {
        // Hammer the first-resolve window: threads race to build the scoped service while the scope is torn
        // down. ObjectDisposedException is the only acceptable failure.
        using var container = TestContainer.Build(b => b.Register<Disposable>().AsScoped());

        for (var round = 0; round < 200; round++)
        {
            var scope = container.CreateScope();
            using var start = new ManualResetEventSlim();

            var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                start.Wait();
                try { scope.Resolve<Disposable>(); }
                catch (ObjectDisposedException) { }
            })).ToArray();

            start.Set();
            scope.Dispose();
            await Task.WhenAll(workers);
        }
    }

    [Fact]
    public void Scopes_do_not_dispose_their_children_by_default()
    {
        using var container = TestContainer.Build(b => b.Register<Disposable>().AsScoped());
        var parent = container.CreateScope();
        var child = parent.CreateScope();
        var inChild = child.Resolve<Disposable>();

        parent.Dispose();

        Assert.False(inChild.Disposed);
        child.Dispose();
        Assert.True(inChild.Disposed);
    }

    [Fact]
    public void With_the_option_on_disposing_a_scope_disposes_its_children_first()
    {
        var log = new List<string>();
        var names = new Queue<string>(new[] { "parent", "child" });
        using var container = BuildDisposingChildren(b =>
            b.RegisterFactory<LoggedDisposable>(_ => new LoggedDisposable(log, names.Dequeue())).AsScoped());

        var parent = container.CreateScope();
        var child = parent.CreateScope();
        parent.Resolve<LoggedDisposable>();
        child.Resolve<LoggedDisposable>();

        parent.Dispose();

        // The child's instance goes first, then the parent's own.
        Assert.Equal(new[] { "child", "parent" }, log);
        Assert.Throws<ObjectDisposedException>(() => child.Resolve<LoggedDisposable>());
    }

    [Fact]
    public void With_the_option_on_disposing_the_container_disposes_live_scopes()
    {
        var container = BuildDisposingChildren(b => b.Register<Disposable>().AsScoped());

        var scope = container.CreateScope();
        var instance = scope.Resolve<Disposable>();
        container.Dispose();

        Assert.True(instance.Disposed);
    }

    [Fact]
    public async Task With_the_option_on_async_disposal_also_reaches_the_children()
    {
        var container = BuildDisposingChildren(b => b.Register<Disposable>().AsScoped());

        var scope = container.CreateScope();
        var instance = scope.Resolve<Disposable>();
        await container.DisposeAsync();

        Assert.True(instance.Disposed);
    }

    [Fact]
    public void A_scope_disposed_on_its_own_is_not_disposed_twice_by_its_parent()
    {
        var count = 0;
        using var container = BuildDisposingChildren(b =>
            b.RegisterFactory<DisposeCounter>(_ => new DisposeCounter(() => count++)).AsScoped());

        var parent = container.CreateScope();
        var child = parent.CreateScope();
        child.Resolve<DisposeCounter>();
        child.Dispose();
        parent.Dispose();

        Assert.Equal(1, count);
    }

    [Fact]
    public void A_disposed_scope_cannot_adopt_new_children_when_the_option_is_on()
    {
        using var container = BuildDisposingChildren(_ => { });

        var parent = container.CreateScope();
        parent.Dispose();

        Assert.Throws<ObjectDisposedException>(() => parent.CreateScope());
    }
}
