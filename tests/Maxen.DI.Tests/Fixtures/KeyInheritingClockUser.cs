using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>[FromKeyedServices] with no key: wants the clock registered under its own key.</summary>
public class KeyInheritingClockUser
{
    public KeyInheritingClockUser([FromKeyedServices] IClock clock) => Clock = clock;

    public IClock Clock { get; }
}
