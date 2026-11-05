using Maxen.DI.Interception;

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
    public int Count(string item) => item.Length;
}
