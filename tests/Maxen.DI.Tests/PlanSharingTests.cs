using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// A service many others depend on is reached by many paths. The plan must hold it once, not once per path, or the
/// plan for a deep graph grows without limit. Sharing must not change what the plan checks on each path.
/// </summary>
public class PlanSharingTests
{
    // PlanPair<PlanPair<...PlanPairLeaf>>: every level takes the level below twice, so paths double per level.
    private static Type Nest(int depth)
    {
        var type = typeof(PlanPairLeaf);
        for (var i = 0; i < depth; i++) type = typeof(PlanPair<>).MakeGenericType(type);
        return type;
    }

    private static ResolvePlanSize Resolve(int depth, Action<ContainerBuilder> registerLeaf, Lifetime pairs)
    {
        using var container = TestContainer.Build(b =>
        {
            registerLeaf(b);
            b.Register(typeof(PlanPair<>), typeof(PlanPair<>)).WithLifetime(pairs);
        });
        using var scope = container.CreateScope();

        var key = new ServiceKey(Nest(depth));
        var top = scope.Resolve(key);
        var plan = container.FindDescriptor(key)!.Plan!;
        return new ResolvePlanSize(plan.IsUsable, plan.NodeCount, top);
    }

    private sealed record ResolvePlanSize(bool Usable, int Nodes, object Top);

    [Fact]
    public void A_service_reached_by_many_paths_is_planned_once()
    {
        var result = Resolve(12, b => b.Register<PlanPairLeaf>().AsSingleton(), Lifetime.Singleton);

        // One node per level and one for the leaf, where a node per path would be 8191.
        Assert.True(result.Usable);
        Assert.Equal(13, result.Nodes);
    }

    [Fact]
    public void A_deep_graph_of_shared_services_resolves()
    {
        // Forty levels is a million million paths: this only finishes when the plan grows with services, not paths.
        var result = Resolve(40, b => b.Register<PlanPairLeaf>().AsSingleton(), Lifetime.Singleton);

        Assert.True(result.Usable);
        Assert.Equal(41, result.Nodes);
    }

    [Fact]
    public void Sharing_keeps_scoped_and_transient_lifetimes_apart()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<PlanPairLeaf>();
            b.Register(typeof(PlanPair<>), typeof(PlanPair<>)).AsScoped();
        });
        using var scope = container.CreateScope();

        var top = (PlanPair<PlanPair<PlanPairLeaf>>)scope.Resolve(new ServiceKey(Nest(2)));

        // The scoped level is one object however it is reached; the transient leaf is new for each parameter.
        Assert.Same(top.First, top.Second);
        Assert.NotSame(top.First.First, top.First.Second);
    }

    [Fact]
    public void A_plan_that_cannot_share_and_grows_too_large_is_given_up()
    {
        // A factory sees who is asking, so services above one can't be shared between paths. Rather than let the
        // plan double per level, the planner stops and the normal path builds the service.
        var result = Resolve(12, b => b.RegisterFactory<PlanPairLeaf>(_ => new PlanPairLeaf()).AsSingleton(), Lifetime.Singleton);

        Assert.False(result.Usable);
        var top = Assert.IsType<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPair<PlanPairLeaf>>>>>>>>>>>>>(result.Top);
        Assert.Same(top.First, top.Second);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_shared_service_is_still_checked_against_every_path_that_reaches_it(bool plans)
    {
        // The first path to the middle is legal (transient -> transient -> scoped). The second goes through a
        // singleton, which must not hold the scoped leaf, and finding the middle already planned must not hide that.
        using var container = TestContainer.Build(b =>
        {
            b.Options.UseResolvePlans = plans;
            b.Register<PlanPairLeaf>().AsScoped();
            b.Register<PlanSharedMiddle>();
            b.Register<PlanSharedHolder>().AsSingleton();
            b.Register<PlanSharedTop>();
        });
        using var scope = container.CreateScope();

        var error = Assert.Throws<ScopeValidationException>(() => scope.Resolve<PlanSharedTop>());
        Assert.Contains("PlanSharedHolder", error.Message);
    }
}
