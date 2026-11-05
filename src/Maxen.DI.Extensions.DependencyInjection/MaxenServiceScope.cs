using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>A Microsoft <see cref="IServiceScope"/> backed by a Maxen scope.</summary>
internal sealed class MaxenServiceScope : IServiceScope, IAsyncDisposable
{
    private readonly MaxenServiceProvider _provider;

    public MaxenServiceScope(MaxenServiceProvider provider) => _provider = provider;

    public IServiceProvider ServiceProvider => _provider;

    public void Dispose() => _provider.Dispose();

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
