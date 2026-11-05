namespace Maxen.DI.Samples.Generators.Generic;

/// <summary>An open generic decorator: it wraps IShelf of every closed type.</summary>
[Decorator(typeof(IShelf<>))]
public sealed class LabelledShelf<T> : IShelf<T>
{
    private readonly IShelf<T> _inner;

    public LabelledShelf(IShelf<T> inner) => _inner = inner;

    public string Label() => "[" + _inner.Label() + "]";
}
