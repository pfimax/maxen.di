namespace Maxen.DI.Samples.Features.Decorators;

internal sealed class PoliteGreeter : IGreeter
{
    private readonly IGreeter _inner;

    public PoliteGreeter(IGreeter inner) => _inner = inner;

    public string Greet(string name) => "Dear friend. " + _inner.Greet(name);
}
