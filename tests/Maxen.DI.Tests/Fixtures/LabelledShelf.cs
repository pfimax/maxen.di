namespace Maxen.DI.Tests.Fixtures;

/// <summary>Open generic decorator declared by attribute: wraps every IShelf&lt;T&gt;.</summary>
[Decorator(typeof(IShelf<>))]
public sealed class LabelledShelf<T> : IShelf<T>
{
    private readonly IShelf<T> _inner;

    public LabelledShelf(IShelf<T> inner) => _inner = inner;

    public string Label() => "[" + _inner.Label() + "]";
}
