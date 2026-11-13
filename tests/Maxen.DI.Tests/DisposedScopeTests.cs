using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// A scope that has been disposed is over. Asking it for anything is a bug in the caller, and it should hear about
/// it whatever the lifetime of the service, not only when the lookup happens to touch the scope's instances.
/// </summary>
public class DisposedScopeTests
{
    private static Container Build() => TestContainer.Build(b =>
    {
        b.Register<Clock>();
        b.Register<IClock, Clock>().AsSingleton();
        b.Register<Disposable>().AsScoped();
    });

    [Fact]
    public void A_transient_cannot_be_resolved_from_a_disposed_scope()
    {
        using var container = Build();
        var scope = container.CreateScope();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Resolve<Clock>());
    }

    [Fact]
    public void Nor_can_a_singleton_that_already_exists()
    {
        using var container = Build();
        container.Resolve<IClock>();
        var scope = container.CreateScope();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Resolve<IClock>());
    }

    [Fact]
    public void Every_way_of_asking_is_refused()
    {
        using var container = Build();
        var scope = container.CreateScope();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.TryResolve<Clock>());
        Assert.Throws<ObjectDisposedException>(() => scope.ResolveAll<Clock>());
        Assert.Throws<ObjectDisposedException>(() => scope.ResolveWith<Clock>(new Maxen.DI.Arguments.ResolveArguments()));
        Assert.Throws<ObjectDisposedException>(() => ((IServiceProvider)scope).GetService(typeof(Clock)));
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope());
        Assert.Throws<ObjectDisposedException>(() => scope.CreateScope("request"));
    }

    [Fact]
    public async Task Disposing_asynchronously_ends_the_scope_just_the_same()
    {
        await using var container = Build();
        var scope = container.CreateScope();
        await scope.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => scope.Resolve<Clock>());
    }

    [Fact]
    public void A_disposed_container_refuses_too()
    {
        var container = Build();
        container.Dispose();

        Assert.Throws<ObjectDisposedException>(() => container.Resolve<Clock>());
        Assert.Throws<ObjectDisposedException>(() => container.CreateScope());
    }

    [Fact]
    public void A_Lazy_or_Func_that_outlives_its_scope_fails_when_used()
    {
        using var container = Build();
        var scope = container.CreateScope();
        var lazy = scope.Resolve<Lazy<Clock>>();
        var make = scope.Resolve<Func<Clock>>();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => lazy.Value);
        Assert.Throws<ObjectDisposedException>(() => make());
    }

    [Fact]
    public void Disposing_twice_is_harmless_and_other_scopes_carry_on()
    {
        using var container = Build();
        var ended = container.CreateScope();
        using var live = container.CreateScope();

        ended.Dispose();
        ended.Dispose();

        Assert.NotNull(live.Resolve<Clock>());
        Assert.NotNull(container.Resolve<IClock>());
    }
}
