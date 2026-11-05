namespace Maxen.DI.Samples.Generators.Generic;

public sealed class Shelf<T> : IShelf<T>
{
    public string Label() => typeof(T).Name;
}
