namespace Maxen.DI.Samples.Generators.Pricing;

/// <summary>A decorator declared by attribute. The generator wires it up, so there is no builder.Decorate call either.</summary>
[Decorator(typeof(IPriceCalculator))]
public sealed class DiscountPricing : IPriceCalculator
{
    private readonly IPriceCalculator _inner;

    public DiscountPricing(IPriceCalculator inner) => _inner = inner;

    public decimal PriceFor(decimal basePrice) => _inner.PriceFor(basePrice) * 0.9m;
}
