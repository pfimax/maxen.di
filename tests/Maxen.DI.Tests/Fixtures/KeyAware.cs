using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Takes its own key through [ServiceKey]; the parameterless constructor is the fallback when it has none.</summary>
public class KeyAware
{
    public KeyAware() => Key = "none";

    public KeyAware([ServiceKey] string key) => Key = key;

    public string Key { get; }
}
