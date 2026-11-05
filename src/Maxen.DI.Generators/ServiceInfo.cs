namespace Maxen.DI.Generators
{
    /// <summary>
    /// What the generator knows about a registration, kept for cross-checks between classes (for example
    /// "a singleton depends on a scoped service"). Plain strings, so Roslyn can compare it cheaply.
    /// </summary>
    /// <param name="ClassName">The class being registered, fully qualified.</param>
    /// <param name="ServiceName">The service type it is registered as, fully qualified.</param>
    /// <param name="Key">The registration key as C# source, or null.</param>
    /// <param name="Lifetime">Transient, Scoped or Singleton.</param>
    /// <param name="Dependencies">Constructor parameters as "type|key" entries separated by semicolons.</param>
    /// <param name="Location">Where to report problems.</param>
    internal sealed record ServiceInfo(
        string ClassName, string ServiceName, string? Key, string Lifetime, string Dependencies, LocationInfo Location);
}
