using Maxen.DI.Options;
using Microsoft.Extensions.Configuration;

namespace Maxen.DI.Extensions.DependencyInjection;

/// <summary>Fills Maxen.DI settings objects from <c>Microsoft.Extensions.Configuration</c>.</summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Binds a configuration section (appsettings.json, environment variables, ...) onto <typeparamref name="T"/>.
    /// Inject the result as <c>IOptions&lt;T&gt;</c>. It cooperates with <c>Configure&lt;T&gt;</c>: steps run in order,
    /// so a later <c>Configure</c> can still adjust what the file said.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Configuration binding reads and writes properties by reflection.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Configuration binding may need to create generic types at run time.")]
    public static ContainerBuilder BindOptions<T>(this ContainerBuilder builder, IConfiguration section)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(section);

        // Binding happens when IOptions<T> is first resolved, not now, so configuration providers that
        // load after this call are still honoured. The result is a singleton: later reloads are not picked up.
        return builder.Configure<T>(options => section.Bind(options));
    }
}
