namespace Maxen.DI.Tests.Fixtures;

/// <summary>An open generic decorator with a dependency of its own, besides the repository it wraps.</summary>
public class AuditedRepo<T> : IRepo<T>
{
    public AuditedRepo(IRepo<T> inner, Disposable audit)
    {
        Inner = inner;
        Audit = audit;
    }

    public IRepo<T> Inner { get; }
    public Disposable Audit { get; }
}
