namespace Maxen.DI.Options;

/// <summary>
/// The default <see cref="IOptions{T}"/>: creates the settings object once and runs every
/// registered configuration step over it.
/// </summary>
public sealed class Options<T> : IOptions<T> where T : class, new()
{
    /// <summary>Creates the options holder from every registered configuration step.</summary>
    public Options(IEnumerable<IConfigureOptions<T>> configurators)
    {
        // Built eagerly: the container hands us a singleton, and failing fast on a bad
        // configuration step beats failing later at the first read of Value.
        var value = new T();
        foreach (var configurator in configurators)
            configurator.Configure(value);

        Value = value;
    }

    /// <summary>The finished options object, built when <see cref="IOptions{T}"/> is first resolved.</summary>
    public T Value { get; }
}
