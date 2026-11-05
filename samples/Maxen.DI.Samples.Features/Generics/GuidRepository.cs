namespace Maxen.DI.Samples.Features.Generics;

internal sealed class GuidRepository : IRepository<Guid>
{
    public string Describe() => "special repository just for Guid";
}
