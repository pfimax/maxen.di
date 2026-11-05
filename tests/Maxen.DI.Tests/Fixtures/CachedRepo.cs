namespace Maxen.DI.Tests.Fixtures;

/// <summary>Open generic decorator around another repository.</summary>
public class CachedRepo<T> : IRepo<T>
{
    public CachedRepo(IRepo<T> inner) => Inner = inner;

    public IRepo<T> Inner { get; }
}
