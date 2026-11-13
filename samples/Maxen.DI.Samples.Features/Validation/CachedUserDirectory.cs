namespace Maxen.DI.Samples.Features.Validation;

/// <summary>A decorator with a dependency of its own, besides the directory it wraps.</summary>
internal sealed class CachedUserDirectory : IUserDirectory
{
    public CachedUserDirectory(IUserDirectory inner, Database database)
    {
        Inner = inner;
        Database = database;
    }

    public IUserDirectory Inner { get; }
    public Database Database { get; }
}
