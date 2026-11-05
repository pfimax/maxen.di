namespace Maxen.DI.Tests.Fixtures;

/// <summary>A greeter that needs nothing, handy for keyed-service tests.</summary>
public sealed class LoudGreeterWithoutDependencies : IGreeter
{
    public string Greet() => "HELLO";
}
