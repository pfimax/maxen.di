using Maxen.DI.Pipeline;

namespace Maxen.DI.Extensibility;

/// <summary>
/// Runs on every object the container constructs, right after construction. Use it for things
/// like property injection or calling an init method.
/// </summary>
public interface IInstanceInitializer
{
    /// <summary>Runs on a freshly built instance, before it is handed out.</summary>
    void Initialize(object instance, ResolveContext context);
}
