using Maxen.DI.Pipeline;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ChildContainerTests
{
    [Fact]
    public void A_child_falls_back_to_its_parent()
    {
        using var parent = TestContainer.Build(b => b.Register<IClock, Clock>().AsSingleton());
        using var child = parent.CreateChild(_ => { });

        Assert.Same(parent.Resolve<IClock>(), child.Resolve<IClock>());
    }

    [Fact]
    public void The_childs_registrations_win_without_touching_the_parent()
    {
        using var parent = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });
        using var child = parent.CreateChild(b => b.Register<IGreeter, QuietGreeter>());

        Assert.IsType<QuietGreeter>(child.Resolve<IGreeter>());
        Assert.IsType<Greeter>(parent.Resolve<IGreeter>());
    }

    [Fact]
    public void Parent_services_can_be_used_by_child_services()
    {
        using var parent = TestContainer.Build(b => b.Register<IClock, Clock>());
        using var child = parent.CreateChild(b => b.Register<IGreeter, Greeter>());

        Assert.NotNull(((Greeter)child.Resolve<IGreeter>()).Clock);
    }

    [Fact]
    public void Collections_combine_parent_and_child_registrations()
    {
        using var parent = TestContainer.Build(b => b.Register<IGreeter, QuietGreeter>());
        using var child = parent.CreateChild(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });

        Assert.Equal(2, child.ResolveAll<IGreeter>().Count);
        Assert.Single(parent.ResolveAll<IGreeter>());
    }

    [Fact]
    public void Disposing_a_child_leaves_the_parents_singletons_alone()
    {
        using var parent = TestContainer.Build(b => b.Register<Disposable>().AsSingleton());
        var fromParent = parent.Resolve<Disposable>();

        var child = parent.CreateChild(b => b.Register<NeedsDisposable>().AsSingleton());
        child.Resolve<NeedsDisposable>();
        child.Dispose();

        Assert.False(fromParent.Disposed);
    }

    [Fact]
    public void Children_inherit_the_parents_extension_setup()
    {
        var log = new List<string>();
        using var parent = TestContainer.Build(b =>
            b.Use(MiddlewarePhase.PreLifetime, (ctx, next) =>
            {
                log.Add(ctx.Key.ToString());
                return next(ctx);
            }));
        using var child = parent.CreateChild(b => b.Register<Clock>());

        child.Resolve<Clock>();

        Assert.Contains("Clock", log);
    }

    [Fact]
    public void Lazy_in_a_child_can_see_parent_services()
    {
        using var parent = TestContainer.Build(b => b.Register<IClock, Clock>());
        using var child = parent.CreateChild(_ => { });

        Assert.NotNull(child.Resolve<Lazy<IClock>>().Value);
    }
}
