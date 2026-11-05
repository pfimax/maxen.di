using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// The resolve plan is a shortcut, so its whole contract is "same answers as the pipeline". Each scenario runs with
/// plans on and off and must behave identically; a few tests also check the plan really is used where it should be.
/// </summary>
public class ResolvePlanTests
{
    public static IEnumerable<object[]> Modes() => new[] { new object[] { true }, new object[] { false } };

    private static Container Build(bool plans, Action<ContainerBuilder> configure)
        => TestContainer.Build(b =>
        {
            b.Options.UseResolvePlans = plans;
            configure(b);
        });

    private static void Register(ContainerBuilder b, Lifetime top, Lifetime middle, Lifetime leaf)
    {
        b.Register<PlanTop>().WithLifetime(top);
        b.Register<PlanMiddle>().WithLifetime(middle);
        b.Register<PlanLeaf>().WithLifetime(leaf);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Transient_chains_build_fresh_objects_every_time(bool plans)
    {
        using var container = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Transient, Lifetime.Transient));
        using var scope = container.CreateScope();

        var first = scope.Resolve<PlanTop>();
        var second = scope.Resolve<PlanTop>();

        Assert.NotSame(first, second);
        Assert.NotSame(first.Leaf, first.Middle.Leaf);
        Assert.Equal("hello", first.Middle.Greeting);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Scoped_and_singleton_services_are_shared_the_same_way(bool plans)
    {
        using var container = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Singleton, Lifetime.Scoped));
        // The singleton middle would capture the scoped leaf, so use a legal mix instead.
        using var legal = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Scoped, Lifetime.Singleton));
        using var one = legal.CreateScope();
        using var two = legal.CreateScope();

        var a = one.Resolve<PlanTop>();
        var b = one.Resolve<PlanTop>();
        var c = two.Resolve<PlanTop>();

        Assert.Same(a.Middle, b.Middle);
        Assert.NotSame(a.Middle, c.Middle);
        Assert.Same(a.Leaf, c.Leaf);
        Assert.Same(a.Middle.Leaf, a.Leaf);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void A_scoped_service_cannot_be_resolved_from_the_root(bool plans)
    {
        using var container = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Scoped, Lifetime.Transient));

        var error = Assert.Throws<ScopeValidationException>(() => container.Resolve<PlanTop>());
        Assert.Contains("PlanMiddle", error.Message);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void A_singleton_holding_a_scoped_service_fails_with_the_same_message(bool plans)
    {
        using var container = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Singleton, Lifetime.Scoped));
        using var scope = container.CreateScope();

        var error = Assert.Throws<ScopeValidationException>(() => scope.Resolve<PlanTop>());
        Assert.Contains("Singleton service", error.Message);
        Assert.Contains("PlanTop -> PlanMiddle -> PlanLeaf", error.Message);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Missing_dependencies_report_the_same_error(bool plans)
    {
        using var container = Build(plans, b => b.Register<PlanMiddle>());

        // The leaf is not registered and has no default, so the constructor cannot be satisfied.
        Assert.ThrowsAny<Exception>(() => container.Resolve<PlanMiddle>());
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Disposal_happens_once_for_everything_that_was_built(bool plans)
    {
        var createdBefore = PlanLeaf.Created;
        var disposedBefore = PlanLeaf.Disposed;

        var container = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Transient, Lifetime.Transient));
        var scope = container.CreateScope();
        scope.Resolve<PlanTop>();
        scope.Resolve<PlanTop>();
        scope.Dispose();
        container.Dispose();

        // Two tops, each with two leaves.
        Assert.Equal(4, PlanLeaf.Created - createdBefore);
        Assert.Equal(4, PlanLeaf.Disposed - disposedBefore);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void A_child_container_sees_its_own_overrides_but_shares_parent_singletons(bool plans)
    {
        using var parent = Build(plans, b => Register(b, Lifetime.Transient, Lifetime.Transient, Lifetime.Singleton));
        using var child = parent.CreateChild(b => b.Register<PlanMiddle>().AsSingleton());

        var fromParent = parent.Resolve<PlanTop>();
        var fromChild = child.Resolve<PlanTop>();

        // PlanTop belongs to the parent, so it builds with the parent's middle, not the child's override.
        Assert.NotSame(fromParent.Middle, fromChild.Middle);
        Assert.Same(fromParent.Leaf, fromChild.Leaf);
        Assert.Same(child.Resolve<PlanMiddle>(), child.Resolve<PlanMiddle>());
    }

    [Fact]
    public void Plain_registrations_get_a_plan()
    {
        using var container = Build(true, b => Register(b, Lifetime.Transient, Lifetime.Scoped, Lifetime.Singleton));
        using var scope = container.CreateScope();
        scope.Resolve<PlanTop>();

        Assert.True(container.FindDescriptor(new ServiceKey(typeof(PlanTop)))!.Plan!.IsUsable);
    }

    [Fact]
    public void Plans_stay_out_when_middleware_could_be_skipped()
    {
        using var container = Build(true, b =>
        {
            b.UseTelemetry();
            Register(b, Lifetime.Transient, Lifetime.Transient, Lifetime.Transient);
        });
        container.Resolve<PlanTop>();

        Assert.False(container.FindDescriptor(new ServiceKey(typeof(PlanTop)))!.Plan is { IsUsable: true });
    }

    [Fact]
    public void Plans_stay_out_for_registrations_with_a_condition()
    {
        using var container = Build(true, b =>
        {
            b.Register<PlanTop>();
            b.Register<PlanMiddle>();
            b.Register<PlanLeaf>();
            // The conditional one is registered last, so it is the winner the plan would have to evaluate.
            b.Register<PlanLeaf>().WhenInjectedInto<PlanMiddle>();
        });
        using var scope = container.CreateScope();
        scope.Resolve<PlanTop>();

        Assert.False(container.FindDescriptor(new ServiceKey(typeof(PlanTop)))!.Plan!.IsUsable);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Lazy_and_Func_parameters_defer_the_work_the_same_way(bool plans)
    {
        using var container = Build(plans, b =>
        {
            b.Register<PlanDeferredUser>();
            b.Register<PlanLeaf>().AsScoped();
        });
        using var scope = container.CreateScope();
        var before = PlanLeaf.Created;

        var user = scope.Resolve<PlanDeferredUser>();
        Assert.Equal(before, PlanLeaf.Created);
        Assert.False(user.Lazy.IsValueCreated);

        // Scoped, so Lazy and Func hand out the one instance of this scope.
        var viaLazy = user.Lazy.Value;
        Assert.Same(viaLazy, user.Factory());
        Assert.Same(viaLazy, scope.Resolve<PlanLeaf>());
        Assert.Equal(before + 1, PlanLeaf.Created);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void Func_of_a_transient_builds_a_new_one_per_call(bool plans)
    {
        using var container = Build(plans, b =>
        {
            b.Register<PlanDeferredUser>();
            b.Register<PlanLeaf>();
        });
        using var scope = container.CreateScope();

        var user = scope.Resolve<PlanDeferredUser>();

        Assert.NotSame(user.Factory(), user.Factory());
    }

    [Fact]
    public void Lazy_and_Func_do_not_stop_a_registration_from_using_a_plan()
    {
        using var container = Build(true, b =>
        {
            b.Register<PlanDeferredUser>();
            b.Register<PlanLeaf>();
        });
        using var scope = container.CreateScope();
        scope.Resolve<PlanDeferredUser>();

        Assert.True(container.FindDescriptor(new ServiceKey(typeof(PlanDeferredUser)))!.Plan!.IsUsable);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void A_factory_below_a_planned_service_is_used_and_can_resolve_its_own_dependencies(bool plans)
    {
        using var container = Build(plans, b =>
        {
            b.Register<PlanTop>();
            b.Register<PlanLeaf>();
            b.RegisterFactory<PlanMiddle>(ctx => new PlanMiddle(ctx.Resolve<PlanLeaf>(), "from factory")).AsScoped();
        });
        using var scope = container.CreateScope();

        var one = scope.Resolve<PlanTop>();
        var two = scope.Resolve<PlanTop>();

        Assert.Equal("from factory", one.Middle.Greeting);
        Assert.Same(one.Middle, two.Middle);
        Assert.NotSame(one.Leaf, two.Leaf);
    }

    [Fact]
    public void A_registration_with_a_factory_below_it_still_gets_a_plan()
    {
        using var container = Build(true, b =>
        {
            b.Register<PlanTop>();
            b.Register<PlanLeaf>();
            b.RegisterFactory<PlanMiddle>(ctx => new PlanMiddle(ctx.Resolve<PlanLeaf>()));
        });
        using var scope = container.CreateScope();
        scope.Resolve<PlanTop>();

        Assert.True(container.FindDescriptor(new ServiceKey(typeof(PlanTop)))!.Plan!.IsUsable);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void A_loop_that_runs_through_a_factory_is_still_reported(bool plans)
    {
        using var container = Build(plans, b =>
        {
            b.Register<PlanTop>();
            // The factory asks for the very service that needs it.
            b.RegisterFactory<PlanMiddle>(ctx => new PlanMiddle(ctx.Resolve<PlanLeaf>(), ctx.Resolve<PlanTop>().ToString()!));
            b.Register<PlanLeaf>();
        });
        using var scope = container.CreateScope();

        Assert.Throws<CircularDependencyException>(() => scope.Resolve<PlanTop>());
    }
}
