using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests.Fixtures;

/// <summary>A generator-registered class that uses Microsoft's key attributes instead of [FromKey].</summary>
[Service(Key = "fast", ServiceType = typeof(MicrosoftAttributedThing))]
public class MicrosoftAttributedThing
{
    public MicrosoftAttributedThing([ServiceKey] string key, [FromKeyedServices] IClock sameKeyClock, [FromKeyedServices(null)] IClock plainClock)
    {
        Key = key;
        SameKeyClock = sameKeyClock;
        PlainClock = plainClock;
    }

    public string Key { get; }

    public IClock SameKeyClock { get; }

    public IClock PlainClock { get; }
}
