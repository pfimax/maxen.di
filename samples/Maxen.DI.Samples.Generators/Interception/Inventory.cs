using Maxen.DI.Interception;
using Maxen.DI.Samples.Generators.Keyed;

namespace Maxen.DI.Samples.Generators.Interception;

/// <summary>
/// [Intercept] puts interceptors in front of the service. The proxy class that does it is written at compile time,
/// so nothing is emitted at run time and it works with native AOT. Order 1 runs outside Order 2.
/// </summary>
[Service(ServiceLifetime.Scoped, ServiceType = typeof(IInventory))]
[Intercept(typeof(CallCounter), Order = 1)]
[Intercept(typeof(AuditTrail), Order = 2)]
public sealed class Inventory : IInventory
{
    private readonly IStorage _storage;

    // The generator writes "new Inventory(Resolve<IStorage>("disk"))" and notes the dependency on the registration,
    // so validation and the dependency graph know this class needs the disk storage.
    public Inventory([FromKey("disk")] IStorage storage) => _storage = storage;

    public int Count(string item) => item.Length;

    public string StoredIn => _storage.Where;
}
