namespace Maxen.DI.Tests.Fixtures;

/// <summary>Decorator: shouts whatever the greeter it wraps says.</summary>
public class LoudGreeter : IGreeter
{
    public LoudGreeter(IGreeter inner) => Inner = inner;

    public IGreeter Inner { get; }

    public string Greet() => Inner.Greet().ToUpperInvariant();
}
