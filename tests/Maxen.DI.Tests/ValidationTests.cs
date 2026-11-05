using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ValidationTests
{
    [Fact]
    public void Scoped_service_cannot_be_resolved_from_the_root()
    {
        using var container = TestContainer.Build(b => b.Register<Disposable>().AsScoped());

        Assert.Throws<ScopeValidationException>(() => container.Resolve<Disposable>());
    }

    [Fact]
    public void Singleton_cannot_capture_a_scoped_service()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<Disposable>().AsScoped();
            b.Register<NeedsDisposable>().AsSingleton();
        });
        using var scope = container.CreateScope();

        Assert.Throws<ScopeValidationException>(() => scope.Resolve<NeedsDisposable>());
    }

    [Fact]
    public void Circular_dependencies_are_reported_with_their_path()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<A>();
            b.Register<B>();
        });

        var error = Assert.Throws<CircularDependencyException>(() => container.Resolve<A>());

        Assert.Contains("A -> B -> A", error.Message);
    }

    [Fact]
    public void Missing_dependencies_are_named_in_the_error()
    {
        using var container = TestContainer.Build(b => b.Register<IGreeter, Greeter>());

        var error = Assert.Throws<ResolutionException>(() => container.Resolve<IGreeter>());

        Assert.Contains("IClock", error.Message);
    }

    [Fact]
    public void IServiceProvider_returns_null_for_unknown_services()
    {
        using var container = TestContainer.Build(_ => { });

        Assert.Null(((IServiceProvider)container).GetService(typeof(IClock)));
    }
}
