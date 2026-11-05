namespace Maxen.DI.Samples.Generators.Keyed;

[Service(ServiceLifetime.Singleton, ServiceType = typeof(IStorage), Key = "cloud")]
public sealed class CloudStorage : IStorage
{
    public string Where => "cloud";
}
