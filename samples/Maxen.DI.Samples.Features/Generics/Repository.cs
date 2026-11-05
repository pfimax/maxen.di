namespace Maxen.DI.Samples.Features.Generics;

internal sealed class Repository<T> : IRepository<T>
{
    public string Describe() => "repository of " + typeof(T).Name;
}
