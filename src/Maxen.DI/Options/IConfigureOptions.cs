namespace Maxen.DI.Options;

/// <summary>One step in setting up a settings object. Steps run in the order they were registered.</summary>
public interface IConfigureOptions<in T> where T : class
{
    /// <summary>Changes the options object before it is handed out.</summary>
    void Configure(T options);
}
