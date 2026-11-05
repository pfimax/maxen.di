namespace Maxen.DI.Samples.Generators.Pricing;

/// <summary>
/// Registered by the source generator from this attribute alone: no builder.Register call exists for it.
/// ServiceType says which interface it answers to; the lifetime is the first argument.
/// </summary>
[Service(ServiceLifetime.Singleton, ServiceType = typeof(IPriceCalculator))]
public sealed class StandardPricing : IPriceCalculator
{
    public decimal PriceFor(decimal basePrice) => basePrice;
}
