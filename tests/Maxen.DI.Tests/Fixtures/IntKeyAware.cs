using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Only works with an int key, so any other key has to fail loudly rather than be cast.</summary>
public class IntKeyAware
{
    public IntKeyAware([ServiceKey] int key) => Key = key;

    public int Key { get; }
}
