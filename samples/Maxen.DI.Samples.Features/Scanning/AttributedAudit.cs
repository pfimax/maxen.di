namespace Maxen.DI.Samples.Features.Scanning;

/// <summary>Marked with [Service], so RegisterAttributed (reflection) or the source generator picks it up.</summary>
[Service(ServiceLifetime.Singleton)]
public sealed class AttributedAudit
{
    public string Describe() => "found through the [Service] attribute";
}
