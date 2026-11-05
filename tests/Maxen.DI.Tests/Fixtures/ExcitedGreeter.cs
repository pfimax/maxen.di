namespace Maxen.DI.Tests.Fixtures;

/// <summary>Decorator: adds an exclamation mark, so tests can tell in which order decorators were applied.</summary>
public class ExcitedGreeter : IGreeter
{
    public ExcitedGreeter(IGreeter inner) => Inner = inner;

    public IGreeter Inner { get; }

    public string Greet() => Inner.Greet() + "!";
}
