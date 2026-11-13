using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ValidationExtrasTests
{
    private static Container BuildWithFactoryChecks(Action<ContainerBuilder> configure)
    {
        var builder = new ContainerBuilder();
        builder.Options.ValidateFactories = true;
        configure(builder);
        return builder.Build();
    }

    [Fact]
    public void Factories_are_left_alone_unless_asked_for()
    {
        var ran = false;
        using var container = TestContainer.Build(b =>
            b.RegisterFactory<IClock>(_ => { ran = true; return new Clock(); }));

        Assert.Empty(container.Validate());
        Assert.False(ran);
    }

    [Fact]
    public void A_factory_that_asks_for_something_missing_is_reported()
    {
        // The constructor-based check can't see inside the lambda; running it can.
        using var container = BuildWithFactoryChecks(b =>
            b.RegisterFactory<IGreeter>(ctx => new Greeter(ctx.Container.Resolve<IClock>())));

        var issue = Assert.Single(container.Validate());

        Assert.Equal(ValidationIssueKind.FactoryFailed, issue.Kind);
        Assert.Contains("IClock", issue.Message);
    }

    [Fact]
    public void A_healthy_factory_passes_the_dry_run()
    {
        using var container = BuildWithFactoryChecks(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.RegisterFactory<IGreeter>(ctx => new Greeter(ctx.Container.Resolve<IClock>())).AsScoped();
        });

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void ValidateOnBuild_fails_the_build_for_a_broken_factory()
    {
        var builder = new ContainerBuilder();
        builder.Options.ValidateOnBuild = true;
        builder.Options.ValidateFactories = true;
        builder.RegisterFactory<IClock>(_ => throw new InvalidOperationException("no clock today"));

        var error = Assert.Throws<ContainerValidationException>(() => builder.Build());

        Assert.Contains("no clock today", error.Message);
    }

    [Fact]
    public void A_factory_scoped_to_a_label_is_run_inside_a_scope_with_that_label()
    {
        using var container = BuildWithFactoryChecks(b =>
            b.RegisterFactory<IClock>(_ => new Clock()).AsScopedTo("request"));

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void A_factory_may_ask_for_services_scoped_to_any_label_in_use()
    {
        // The factory itself is plainly scoped; what it resolves needs two different labelled scopes around it.
        using var container = BuildWithFactoryChecks(b =>
        {
            b.Register<Clock>().AsScopedTo("request");
            b.Register<Disposable>().AsScopedTo("tenant");
            b.RegisterFactory<IClock>(ctx =>
            {
                ctx.Resolve<Disposable>();
                return ctx.Resolve<Clock>();
            }).AsScoped();
        });

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void What_factory_checks_build_in_labelled_scopes_is_disposed_with_them()
    {
        var disposed = 0;
        using var container = BuildWithFactoryChecks(b =>
            b.RegisterFactory<DisposeCounter>(_ => new DisposeCounter(() => disposed++)).AsScopedTo("request"));

        Assert.Empty(container.Validate());
        Assert.Equal(1, disposed);
    }

    [Fact]
    public void A_build_that_fails_validation_disposes_what_validation_built()
    {
        // The first factory works and its singleton is created; the second fails the build. Nobody ever gets the
        // container, so nobody else could dispose that singleton.
        var created = 0;
        var disposed = 0;
        var builder = new ContainerBuilder();
        builder.Options.ValidateOnBuild = true;
        builder.Options.ValidateFactories = true;
        builder.RegisterFactory<DisposeCounter>(_ =>
        {
            created++;
            return new DisposeCounter(() => disposed++);
        }).AsSingleton();
        builder.RegisterFactory<IClock>(_ => throw new InvalidOperationException("no clock today"));

        Assert.Throws<ContainerValidationException>(() => builder.Build());

        Assert.Equal(1, created);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public void A_registration_limited_to_an_unregistered_consumer_is_reported()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>();
        });

        var issue = Assert.Single(container.Validate());

        Assert.Equal(ValidationIssueKind.UnreachableRegistration, issue.Kind);
        Assert.Contains("Greeter", issue.Message);
    }

    [Fact]
    public void A_registration_limited_to_a_registered_consumer_is_fine()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>();
            b.Register<IGreeter, Greeter>();
        });

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void A_child_may_limit_a_registration_to_a_consumer_registered_in_the_parent()
    {
        using var parent = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });
        using var child = parent.CreateChild(b => b.Register<IClock, FrozenClock>().WhenInjectedInto<Greeter>());

        Assert.Empty(child.Validate());
    }

    [Fact]
    public void A_derived_container_for_a_plugin_shares_singletons_and_leaves_the_parent_untouched()
    {
        using var host = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IGreeter, Greeter>();
        });

        // "Hot add": the host keeps running while a plugin container layers extra registrations on top.
        using var withPlugin = host.CreateChild(b => b.Register<IGreeter, QuietGreeter>());

        Assert.Same(host.Resolve<IClock>(), withPlugin.Resolve<IClock>());
        Assert.IsType<QuietGreeter>(withPlugin.Resolve<IGreeter>());
        Assert.IsType<Greeter>(host.Resolve<IGreeter>());
    }
}
