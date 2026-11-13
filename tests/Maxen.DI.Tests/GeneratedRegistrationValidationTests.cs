using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// The source generator turns a [Service] class into a factory, and validation can't look inside a factory. The
/// generated registration says what it resolves, so these classes are checked like any registered by type.
/// </summary>
public class GeneratedRegistrationValidationTests
{
    // The fixtures hold two [Service] classes: GeneratedThing(IClock, IEnumerable<IGreeter>), a singleton, and
    // KeyedGeneratedThing([FromKey("fast")] IClock), registered under the key "special".
    private static Container Build(Action<ContainerBuilder> configure)
        => TestContainer.Build(b =>
        {
            b.RegisterGeneratedServices();
            configure(b);
        });

    [Fact]
    public void A_generated_registration_with_everything_it_needs_passes()
    {
        using var container = Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IClock, FrozenClock>().WithKey("fast");
        });

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void What_a_generated_registration_needs_is_reported_when_missing()
    {
        using var container = Build(_ => { });

        var issues = container.Validate();

        // One for GeneratedThing, one for KeyedGeneratedThing, and two for MicrosoftAttributedThing, which is registered
        // under "fast" and takes the clock with its own key ([FromKeyedServices]) as well as the plain one.
        Assert.Equal(4, issues.Count);
        Assert.All(issues, i => Assert.Equal(ValidationIssueKind.MissingDependency, i.Kind));
        Assert.Contains(issues, i => i.Message.StartsWith("IGeneratedThing:") && i.Message.Contains("'IClock'"));
        Assert.Contains(issues, i => i.Message.StartsWith("IGeneratedThing (key: special):") && i.Message.Contains("'IClock (key: fast)'"));
        Assert.Contains(issues, i => i.Message.StartsWith("MicrosoftAttributedThing (key: fast):") && i.Message.Contains("'IClock (key: fast)'"));
        Assert.Contains(issues, i => i.Message.StartsWith("MicrosoftAttributedThing (key: fast):") && i.Message.Contains("'IClock'"));
    }

    [Fact]
    public void A_generated_singleton_may_not_hold_a_scoped_service()
    {
        using var container = Build(b =>
        {
            b.Register<IClock, Clock>().AsScoped();
            b.Register<IClock, FrozenClock>().WithKey("fast");
        });

        var issue = Assert.Single(container.Validate());

        Assert.Equal(ValidationIssueKind.LifetimeMismatch, issue.Kind);
        Assert.Contains("IGeneratedThing", issue.Message);
        Assert.Contains("IClock", issue.Message);
    }

    [Fact]
    public void Its_dependencies_show_in_the_graph()
    {
        using var container = Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IClock, FrozenClock>().WithKey("fast");
            b.Register<IGreeter, Greeter>();
        });

        var edges = container.BuildGraph().Edges.Select(e => $"{e.From.Key} -> {e.To.Key}").ToList();

        Assert.Contains("IGeneratedThing -> IClock", edges);
        Assert.Contains("IGeneratedThing -> IGreeter", edges);
        Assert.Contains("IGeneratedThing (key: special) -> IClock (key: fast)", edges);
    }

    [Fact]
    public void A_factory_written_by_hand_can_declare_what_it_resolves_too()
    {
        using var container = TestContainer.Build(b =>
            b.RegisterFactory<IGreeter>(ctx => new Greeter(ctx.Resolve<IClock>()))
                .WithMetadata(ServiceMetadata.Dependencies, new[] { ServiceKey.For<IClock>() }));

        var issue = Assert.Single(container.Validate());
        Assert.Contains("its factory resolves 'IClock', which is not registered", issue.Message);
    }

    [Fact]
    public void A_dependency_on_the_registrations_own_key_follows_that_key()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().WithKey("eu");
            b.RegisterFactory<IGreeter>(ctx => new Greeter(ctx.Resolve<IClock>("eu")))
                .WithKey("eu")
                .WithMetadata(ServiceMetadata.Dependencies, new[] { new ServiceKey(typeof(IClock), ServiceMetadata.SameKey) });
            b.RegisterFactory<IGreeter>(ctx => new Greeter(ctx.Resolve<IClock>("us")))
                .WithKey("us")
                .WithMetadata(ServiceMetadata.Dependencies, new[] { new ServiceKey(typeof(IClock), ServiceMetadata.SameKey) });
        });

        var issue = Assert.Single(container.Validate());
        Assert.Equal("us", issue.Service.Key);
    }
}
