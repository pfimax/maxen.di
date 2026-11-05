namespace Maxen.DI.Tests.Fixtures;

/// <summary>Registered by the source generator, not by reflection: a singleton with injected dependencies.</summary>
[Service(ServiceLifetime.Singleton, ServiceType = typeof(IGeneratedThing))]
public class GeneratedThing : IGeneratedThing
{
    private readonly IEnumerable<IGreeter> _greeters;

    public GeneratedThing(IClock clock, IEnumerable<IGreeter> greeters)
    {
        Clock = clock;
        _greeters = greeters;
    }

    public IClock Clock { get; }

    public string Describe() => "generated with " + _greeters.Count() + " greeter(s)";
}
