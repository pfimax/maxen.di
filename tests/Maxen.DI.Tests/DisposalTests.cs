using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class DisposalTests
{
    [Fact]
    public void Scope_disposes_what_it_built_but_not_supplied_instances()
    {
        var supplied = new Disposable();
        using var container = TestContainer.Build(b =>
        {
            b.Register<Disposable>().AsScoped();

            // Registered under a different service type so it doesn't replace the scoped one.
            b.RegisterInstance<IDisposable>(supplied);
        });

        var scope = container.CreateScope();
        var built = scope.Resolve<Disposable>();
        scope.Dispose();
        Assert.True(built.Disposed);

        container.Dispose();
        Assert.False(supplied.Disposed);
    }

    [Fact]
    public async Task Async_disposal_reaches_singletons()
    {
        var container = TestContainer.Build(b => b.Register<Disposable>().AsSingleton());
        var singleton = container.Resolve<Disposable>();

        await container.DisposeAsync();

        Assert.True(singleton.Disposed);
    }
}
