namespace Maxen.DI.Samples.Generators.Keyed;

/// <summary>Several implementations of one interface, told apart by Key.</summary>
[Service(ServiceLifetime.Singleton, ServiceType = typeof(IStorage), Key = "disk")]
public sealed class DiskStorage : IStorage
{
    public string Where => "disk";
}
