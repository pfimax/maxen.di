using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void A_trace_records_the_dependency_tree_parents_first()
    {
        var trace = new ResolutionTrace();
        using var container = TestContainer.Build(b =>
        {
            b.Observe(trace);
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
        });

        container.Resolve<IGreeter>();

        var events = trace.Events;
        Assert.Equal(typeof(IGreeter), events[0].Key.Type);
        Assert.Equal(0, events[0].Depth);
        Assert.Equal(typeof(IClock), events[1].Key.Type);
        Assert.Equal(1, events[1].Depth);
        Assert.Contains("  IClock", trace.ToString());
    }

    [Fact]
    public void Failures_are_reported_with_their_exception()
    {
        var trace = new ResolutionTrace();
        using var container = TestContainer.Build(b =>
        {
            b.Observe(trace);
            b.Register<IGreeter, Greeter>(); // IClock is missing
        });

        Assert.ThrowsAny<Exception>(() => container.Resolve<IGreeter>());

        Assert.Contains(trace.Events, e => !e.Succeeded);
    }

    [Fact]
    public void Slow_resolutions_can_be_flagged()
    {
        var slow = new List<ServiceKey>();
        using var container = TestContainer.Build(b =>
        {
            b.ObserveSlowResolutions(TimeSpan.Zero, e => slow.Add(e.Key));
            b.Register<Clock>();
        });

        container.Resolve<Clock>();

        Assert.Contains(ServiceKey.For<Clock>(), slow);
    }

    [Fact]
    public void The_graph_can_be_exported_as_mermaid_dot_and_text()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IGreeter, Greeter>();
        });
        var graph = container.BuildGraph();

        var mermaid = graph.ToMermaid();
        Assert.StartsWith("graph LR", mermaid);
        Assert.Contains("-->", mermaid);
        Assert.Contains("IClock (Singleton)", mermaid);

        Assert.Contains("digraph", graph.ToDot());
        Assert.Contains("needs IClock", graph.ToText());
    }
}
