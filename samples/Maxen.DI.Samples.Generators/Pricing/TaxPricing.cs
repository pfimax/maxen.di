namespace Maxen.DI.Samples.Generators.Pricing;

/// <summary>Priority decides the stacking order between several decorators of the same service.</summary>
[Decorator(typeof(IPriceCalculator), Priority = 10)]
public sealed class TaxPricing : IPriceCalculator
{
    private readonly IPriceCalculator _inner;

    public TaxPricing(IPriceCalculator inner) => _inner = inner;

    public decimal PriceFor(decimal basePrice) => _inner.PriceFor(basePrice) * 1.2m;
}
