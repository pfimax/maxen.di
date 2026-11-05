namespace Maxen.DI.Samples.WebApi;

/// <summary>A Maxen decorator: the standard Microsoft container has no equivalent.</summary>
public sealed class ExcitedGreeter : IGreeter
{
    private readonly IGreeter _inner;

    public ExcitedGreeter(IGreeter inner) => _inner = inner;

    public string Greet(string name) => _inner.Greet(name) + "!";
}
