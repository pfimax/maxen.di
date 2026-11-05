namespace Maxen.DI.Tests.Fixtures;

/// <summary>
/// Adds two numbers. Intercepted by attribute: the order is written the "wrong" way round on purpose, to check that
/// Order decides, not the position in the source.
/// </summary>
[Service(ServiceType = typeof(ITallyService))]
[Intercept(typeof(TimesTwoInterceptor), Order = 1)]
[Intercept(typeof(AddTenInterceptor), Order = 0)]
public sealed class TallyService : ITallyService
{
    public int Add(int a, int b) => a + b;
}
