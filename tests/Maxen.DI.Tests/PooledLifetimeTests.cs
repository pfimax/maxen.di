using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class PooledLifetimeTests
{
    [Fact]
    public void Instances_are_reused_by_later_scopes_and_reset_in_between()
    {
        using var container = TestContainer.Build(b => b.Register<PooledBuffer>().AsPooled());

        PooledBuffer first;
        using (var scope = container.CreateScope())
        {
            first = scope.Resolve<PooledBuffer>();
            Assert.Same(first, scope.Resolve<PooledBuffer>()); // same within a scope
        }

        using var next = container.CreateScope();
        var second = next.Resolve<PooledBuffer>();

        Assert.Same(first, second);
        Assert.Equal(1, second.Resets);
        Assert.False(second.Disposed);
    }

    [Fact]
    public void Concurrent_scopes_get_different_instances()
    {
        using var container = TestContainer.Build(b => b.Register<PooledBuffer>().AsPooled());
        using var a = container.CreateScope();
        using var b = container.CreateScope();

        Assert.NotSame(a.Resolve<PooledBuffer>(), b.Resolve<PooledBuffer>());
    }

    [Fact]
    public void Extras_beyond_the_limit_are_disposed_and_the_container_disposes_idle_ones()
    {
        var container = TestContainer.Build(b => b.Register<PooledBuffer>().AsPooled(maxRetained: 1));
        var scopeA = container.CreateScope();
        var scopeB = container.CreateScope();
        var kept = scopeA.Resolve<PooledBuffer>();
        var extra = scopeB.Resolve<PooledBuffer>();

        scopeA.Dispose();
        scopeB.Dispose();

        Assert.False(kept.Disposed);
        Assert.True(extra.Disposed);

        container.Dispose();
        Assert.True(kept.Disposed);
    }

    [Fact]
    public void Pooled_services_cannot_be_resolved_from_the_root()
    {
        using var container = TestContainer.Build(b => b.Register<PooledBuffer>().AsPooled());

        Assert.Throws<ScopeValidationException>(() => container.Resolve<PooledBuffer>());
    }
}
