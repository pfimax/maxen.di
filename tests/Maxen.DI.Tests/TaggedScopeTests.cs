using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class TaggedScopeTests
{
    [Fact]
    public void Nested_scopes_share_the_instance_of_the_nearest_labelled_scope()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>().AsScopedTo("request"));

        using var request = container.CreateScope("request");
        using var inner = request.CreateScope();

        Assert.Same(request.Resolve<IClock>(), inner.Resolve<IClock>());
    }

    [Fact]
    public void Separate_labelled_scopes_get_separate_instances()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>().AsScopedTo("request"));

        using var first = container.CreateScope("request");
        using var second = container.CreateScope("request");

        Assert.NotSame(first.Resolve<IClock>(), second.Resolve<IClock>());
    }

    [Fact]
    public void Resolving_without_a_matching_label_explains_what_to_do()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>().AsScopedTo("request"));
        using var plain = container.CreateScope();

        var error = Assert.Throws<ScopeValidationException>(() => plain.Resolve<IClock>());
        Assert.Contains("request", error.Message);
    }

    [Fact]
    public void The_instance_is_disposed_with_its_labelled_scope_not_the_nested_one()
    {
        var disposed = 0;
        using var container = TestContainer.Build(b =>
            b.RegisterFactory(typeof(DisposeCounter), _ => new DisposeCounter(() => disposed++)).AsScopedTo("job"));

        var job = container.CreateScope("job");
        var step = job.CreateScope();
        step.Resolve<DisposeCounter>();

        step.Dispose();
        Assert.Equal(0, disposed);

        job.Dispose();
        Assert.Equal(1, disposed);
    }

    [Fact]
    public void A_singleton_cannot_capture_a_labelled_service()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsScopedTo("request");
            b.Register<NeedsClock>().AsSingleton();
        });
        using var request = container.CreateScope("request");

        Assert.Throws<ScopeValidationException>(() => request.Resolve<NeedsClock>());
    }
}
