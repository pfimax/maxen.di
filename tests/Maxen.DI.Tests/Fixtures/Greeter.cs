namespace Maxen.DI.Tests.Fixtures;

/// <summary>A plain service with one dependency.</summary>
public class Greeter : IGreeter
{
    public Greeter(IClock clock) => Clock = clock;

    public IClock Clock { get; }

    public string Greet() => "hello";
}
