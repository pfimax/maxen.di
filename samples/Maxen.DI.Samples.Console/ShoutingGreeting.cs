namespace Maxen.DI.Samples.Console;

/// <summary>A decorator declared with an attribute: no builder.Decorate call is needed anywhere.</summary>
[Decorator(typeof(IGreeting))]
public sealed class ShoutingGreeting : IGreeting
{
    private readonly IGreeting _inner;

    public ShoutingGreeting(IGreeting inner) => _inner = inner;

    public string For(string name) => _inner.For(name).ToUpperInvariant() + "!";
}
