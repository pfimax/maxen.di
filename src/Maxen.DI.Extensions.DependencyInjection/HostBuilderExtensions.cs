using Microsoft.Extensions.Hosting;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>One-line setup for <c>Host.CreateDefaultBuilder()</c>.</summary>
public static class HostBuilderExtensions
{
    /// <summary>Makes the host use Maxen.DI as its container.</summary>
    public static IHostBuilder UseMaxenDI(this IHostBuilder hostBuilder, Action<ContainerBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(hostBuilder);
        return hostBuilder.UseServiceProviderFactory(new MaxenServiceProviderFactory(configure));
    }
}
