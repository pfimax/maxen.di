namespace Maxen.DI.Options;

/// <summary>Registers settings objects on a <see cref="ContainerBuilder"/>.</summary>
public static class OptionsExtensions
{
    /// <summary>
    /// Adds a configuration step for <typeparamref name="T"/>. Call it as often as you like; steps run in order.
    /// Inject the result as <c>IOptions&lt;T&gt;</c>.
    /// </summary>
    public static ContainerBuilder Configure<T>(this ContainerBuilder builder, Action<T> configure)
        where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        // The open IOptions<> registration only needs to exist once, however many types are configured.
        if (!builder.IsRegistered(new ServiceKey(typeof(IOptions<>))))
            builder.Register(typeof(IOptions<>), typeof(Options<>)).AsSingleton();

        builder.RegisterInstance<IConfigureOptions<T>>(new DelegateConfigureOptions<T>(configure));
        return builder;
    }

    /// <summary>Fills <typeparamref name="T"/> from flat key/value pairs, optionally under a section such as <c>"Database"</c>.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Binding reads and writes properties by reflection.")]
    public static ContainerBuilder BindOptions<T>(
        this ContainerBuilder builder,
        IReadOnlyDictionary<string, string?> values,
        string? section = null)
        where T : class, new()
        => builder.Configure<T>(options => OptionsBinder.Bind(options, values, section));
}
