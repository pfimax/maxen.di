using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ContainerValidationTests
{
    [Fact]
    public void A_healthy_configuration_has_no_issues()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IGreeter, Greeter>();
        });

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void Missing_dependencies_are_found_without_resolving_anything()
    {
        using var container = TestContainer.Build(b => b.Register<IGreeter, Greeter>());

        var issue = Assert.Single(container.Validate());

        Assert.Equal(ValidationIssueKind.MissingDependency, issue.Kind);
        Assert.Contains("IClock", issue.Message);
    }

    [Fact]
    public void Loops_are_reported_once()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<A>();
            b.Register<B>();
        });

        var issues = container.Validate().Where(i => i.Kind == ValidationIssueKind.CircularDependency).ToList();

        Assert.Single(issues);
    }

    [Fact]
    public void Captive_dependencies_are_found_even_through_a_transient()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<Disposable>().AsScoped();
            b.Register<NeedsDisposable>(); // transient in the middle
            b.Register<NeedsNeedsDisposable>().AsSingleton();
        });

        var issues = container.Validate();

        Assert.Contains(issues, i => i.Kind == ValidationIssueKind.LifetimeMismatch && i.Service.Type == typeof(NeedsNeedsDisposable));
    }

    [Fact]
    public void Lazy_dependencies_do_not_count_as_loops()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<LazyLoopA>();
            b.Register<LazyLoopB>();
        });

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void ValidateOnBuild_throws_with_every_issue()
    {
        var builder = new ContainerBuilder();
        builder.Options.ValidateOnBuild = true;
        builder.Register<IGreeter, Greeter>();

        var error = Assert.Throws<ContainerValidationException>(() => builder.Build());

        Assert.Single(error.Issues);
    }

    [Fact]
    public void The_graph_exposes_nodes_and_edges()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });

        var graph = container.BuildGraph();

        Assert.Contains(graph.Edges, e => e.From.Key.Type == typeof(IGreeter) && e.To.Key.Type == typeof(IClock));
    }
}
