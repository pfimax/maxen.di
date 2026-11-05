namespace Maxen.DI.Samples.Features.Decorators;

/// <summary>Wraps another greeter; the container passes the greeter it wraps into the constructor.</summary>
internal sealed class ExcitedGreeter : IGreeter
{
    private readonly IGreeter _inner;

    public ExcitedGreeter(IGreeter inner) => _inner = inner;

    public string Greet(string name) => _inner.Greet(name) + "!";
}
