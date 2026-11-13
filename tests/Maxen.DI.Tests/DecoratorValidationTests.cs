using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// A decorator is built along with the service it wraps, so what it needs can break that service just as the
/// service's own constructor can. Validation has to find that before the first resolve does.
/// </summary>
public class DecoratorValidationTests
{
    [Fact]
    public void A_decorator_with_a_missing_dependency_is_reported()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Decorate<IClock, LoggedClock>();
        });

        var issue = Assert.Single(container.Validate());

        Assert.Equal(ValidationIssueKind.MissingDependency, issue.Kind);
        Assert.Contains("LoggedClock", issue.Message);
        Assert.Contains("Disposable", issue.Message);

        // And validation is right: resolving really does fail.
        Assert.Throws<ResolutionException>(() => container.Resolve<IClock>());
    }

    [Fact]
    public void A_decorator_whose_dependencies_are_registered_passes()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<Disposable>();
            b.Register<IClock, Clock>();
            b.Decorate<IClock, LoggedClock>();
        });

        Assert.Empty(container.Validate());
        Assert.IsType<LoggedClock>(container.Resolve<IClock>());
    }

    [Fact]
    public void A_decorator_on_a_singleton_may_not_take_a_scoped_service()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<Disposable>().AsScoped();
            b.Register<IClock, Clock>().AsSingleton();
            b.Decorate<IClock, LoggedClock>();
        });

        var issue = Assert.Single(container.Validate());

        Assert.Equal(ValidationIssueKind.LifetimeMismatch, issue.Kind);
        Assert.Contains("IClock", issue.Message);
        Assert.Contains("Disposable", issue.Message);

        using var scope = container.CreateScope();
        Assert.Throws<ScopeValidationException>(() => scope.Resolve<IClock>());
    }

    [Fact]
    public void Decorators_around_factories_and_instances_are_checked_too()
    {
        using var container = TestContainer.Build(b =>
        {
            b.RegisterFactory<IClock>(_ => new Clock());
            b.RegisterInstance<IRepo<string>>(new Repo<string>());
            b.Decorate<IClock, LoggedClock>();
            b.Decorate(typeof(IRepo<>), typeof(AuditedRepo<>));
        });

        var issues = container.Validate();

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(ValidationIssueKind.MissingDependency, i.Kind));
    }

    [Fact]
    public void An_open_generic_decorator_is_checked_for_each_closed_service()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IRepo<string>, Repo<string>>();
            b.Register<IRepo<int>, Repo<int>>();
            b.Decorate(typeof(IRepo<>), typeof(AuditedRepo<>));
        });

        var issues = container.Validate();

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Contains("AuditedRepo", i.Message));
    }

    [Fact]
    public void A_decorator_limited_to_some_registrations_is_only_checked_for_those()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IClock, Clock>().WithKey("logged");
            b.Decorate<IClock, LoggedClock>(when: d => Equals(d.Key.Key, "logged"));
        });

        var issue = Assert.Single(container.Validate());
        Assert.Equal("logged", issue.Service.Key);
    }

    [Fact]
    public void What_a_decorator_needs_stays_out_of_the_drawn_graph()
    {
        // Diagrams and saved snapshots are made from the edges, and adding to them would change every one.
        using var container = TestContainer.Build(b =>
        {
            b.Register<Disposable>();
            b.Register<IClock, Clock>();
            b.Decorate<IClock, LoggedClock>();
        });

        Assert.Empty(container.BuildGraph().Edges);
    }

    [Fact]
    public void A_child_container_checks_the_decorators_it_adds_to_its_own_registrations()
    {
        using var parent = TestContainer.Build(b => b.Register<IClock, Clock>());
        using var child = parent.CreateChild(c =>
        {
            c.Register<IRepo<string>, Repo<string>>();
            c.Decorate(typeof(IRepo<>), typeof(AuditedRepo<>));
            c.Decorate<IClock, LoggedClock>();
        });

        // IClock is the parent's and is built there, without the child's decorator, so only the repository counts.
        var issue = Assert.Single(child.Validate());
        Assert.Contains("AuditedRepo", issue.Message);
        Assert.IsType<Clock>(child.Resolve<IClock>());
    }
}
