using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>What ASP.NET Core and hosted services use to start a scope (for example one per request).</summary>
internal sealed class MaxenServiceScopeFactory : IServiceScopeFactory
{
    private readonly IContainer _container;

    public MaxenServiceScopeFactory(IContainer container) => _container = container;

    public IServiceScope CreateScope()
        => new MaxenServiceScope(new MaxenServiceProvider(_container.CreateScope()));
}
