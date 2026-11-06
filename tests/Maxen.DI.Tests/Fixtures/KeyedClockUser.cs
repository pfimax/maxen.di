using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>Asks for a keyed clock the Microsoft way, with [FromKeyedServices("fast")].</summary>
public class KeyedClockUser
{
    public KeyedClockUser([FromKeyedServices("fast")] IClock clock) => Clock = clock;

    public IClock Clock { get; }
}
