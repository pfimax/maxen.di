namespace Maxen.DI.Tests.Fixtures;

/// <summary>A keyed registration produced by the generator, with a keyed constructor parameter.</summary>
[Service(Key = "special", ServiceType = typeof(IGeneratedThing))]
public class KeyedGeneratedThing : IGeneratedThing
{
    public KeyedGeneratedThing([FromKey("fast")] IClock clock) => Clock = clock;

    public IClock Clock { get; }

    public string Describe() => "special";
}
