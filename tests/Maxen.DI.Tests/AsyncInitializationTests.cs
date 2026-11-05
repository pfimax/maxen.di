using Maxen.DI.Async;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class AsyncInitializationTests
{
    [Fact]
    public async Task ResolveAsync_waits_for_initialization_and_runs_it_once()
    {
        await using var container = TestContainer.Build(b => b.Register<SlowStarter>().AsSingleton());

        var first = await container.ResolveAsync<SlowStarter>();
        var second = await container.ResolveAsync<SlowStarter>();

        Assert.Same(first, second);
        Assert.True(first.Ready);
        Assert.Equal(1, first.InitializeCalls);
    }

    [Fact]
    public async Task Concurrent_callers_share_one_initialization()
    {
        await using var container = TestContainer.Build(b => b.Register<SlowStarter>().AsSingleton());

        var all = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => container.ResolveAsync<SlowStarter>()));

        Assert.Equal(1, all[0].InitializeCalls);
    }

    [Fact]
    public async Task A_failed_initialization_can_be_retried()
    {
        await using var container = TestContainer.Build(b =>
            b.RegisterFactory(_ => new SlowStarter { FailFirstAttempt = true }).AsSingleton());

        await Assert.ThrowsAsync<InvalidOperationException>(() => container.ResolveAsync<SlowStarter>());
        var retried = await container.ResolveAsync<SlowStarter>();

        Assert.True(retried.Ready);
        Assert.Equal(2, retried.InitializeCalls);
    }

    [Fact]
    public async Task InitializeSingletonsAsync_initializes_dependencies_first()
    {
        await using var container = TestContainer.Build(b =>
        {
            // Registered dependent-first on purpose: order of registration must not matter.
            b.Register<NeedsStarter>().AsSingleton();
            b.Register<SlowStarter>().AsSingleton();
        });

        await container.InitializeSingletonsAsync();

        Assert.True(container.Resolve<NeedsStarter>().StarterWasReady);
        Assert.True(container.Resolve<SlowStarter>().Ready);
    }
}
