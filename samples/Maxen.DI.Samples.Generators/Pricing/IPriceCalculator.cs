namespace Maxen.DI.Samples.Generators.Pricing;

public interface IPriceCalculator
{
    decimal PriceFor(decimal basePrice);
}
