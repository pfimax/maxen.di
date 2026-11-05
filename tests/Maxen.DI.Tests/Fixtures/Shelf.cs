namespace Maxen.DI.Tests.Fixtures;

/// <summary>Closed implementation of the open generic shelf; labelled with the item type.</summary>
public sealed class Shelf<T> : IShelf<T>
{
    public string Label() => typeof(T).Name;
}
