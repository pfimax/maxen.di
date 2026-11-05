using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>Hammers the container from many threads at once, where races in caches and locks would show up.</summary>
public class ConcurrencyTests
{
    private const int Threads = 32;

    [Fact]
    public void A_singleton_is_built_once_even_when_many_threads_ask_at_the_same_moment()
    {
        var built = 0;
        using var container = TestContainer.Build(b =>
            b.RegisterFactory(typeof(IClock), _ =>
            {
                Interlocked.Increment(ref built);

                // Slow on purpose, to give other threads time to pile up behind the first one.
                Thread.Sleep(20);
                return new Clock();
            }).AsSingleton());

        var results = RunInParallel(() => container.Resolve<IClock>());

        Assert.Equal(1, built);
        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public void Scopes_never_share_scoped_instances_under_load()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>().AsScoped());

        var pairs = RunInParallel(() =>
        {
            using var scope = container.CreateScope();
            return (First: scope.Resolve<IClock>(), Second: scope.Resolve<IClock>());
        });

        // Inside a scope the instance is stable; across scopes it is unique.
        Assert.All(pairs, p => Assert.Same(p.First, p.Second));
        Assert.Equal(Threads, pairs.Select(p => p.First).Distinct().Count());
    }

    [Fact]
    public void A_store_that_outgrows_its_small_array_still_returns_the_same_instances()
    {
        const int services = 40;
        using var container = TestContainer.Build(b =>
        {
            for (var i = 0; i < services; i++)
                b.Register<IClock, Clock>().WithKey(i).AsSingleton();
        });

        var all = RunInParallel(() =>
            Enumerable.Range(0, services).Select(i => container.Resolve<IClock>(i)).ToArray());

        for (var i = 0; i < services; i++)
            Assert.All(all, set => Assert.Same(all[0][i], set[i]));
    }

    [Fact]
    public void Creating_and_disposing_scopes_in_parallel_disposes_every_instance_exactly_once()
    {
        var created = 0;
        var disposed = 0;
        using var container = TestContainer.Build(b =>
            b.RegisterFactory(typeof(DisposeCounter), _ =>
            {
                Interlocked.Increment(ref created);
                return new DisposeCounter(() => Interlocked.Increment(ref disposed));
            }).AsScoped());

        RunInParallel(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                using var scope = container.CreateScope();
                scope.Resolve<DisposeCounter>();
            }
            return 0;
        });

        Assert.Equal(Threads * 50, created);
        Assert.Equal(created, disposed);
    }

    [Fact]
    public void Resolving_from_a_scope_that_is_being_disposed_fails_cleanly_or_succeeds()
    {
        using var container = TestContainer.Build(b => b.Register<IClock, Clock>().AsScoped());

        // A race between resolve and dispose must never produce anything except a result or ObjectDisposedException.
        RunInParallel(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                var scope = container.CreateScope();
                var resolver = Task.Run(() =>
                {
                    try { scope.Resolve<IClock>(); }
                    catch (ObjectDisposedException) { }
                });
                scope.Dispose();
                resolver.Wait();
            }
            return 0;
        });
    }

    /// <summary>Starts every worker together so they actually overlap, then returns what each produced.</summary>
    private static T[] RunInParallel<T>(Func<T> work)
    {
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, Threads)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                start.Wait();
                return work();
            }, TaskCreationOptions.LongRunning))
            .ToArray();

        start.Set();
        Task.WaitAll(tasks);
        return tasks.Select(t => t.Result).ToArray();
    }
}
