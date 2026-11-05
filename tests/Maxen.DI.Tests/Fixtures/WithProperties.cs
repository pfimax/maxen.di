namespace Maxen.DI.Tests.Fixtures;

/// <summary>Uses property injection, one required and one optional.</summary>
public class WithProperties
{
    [Inject]
    public IClock? Clock { get; set; }

    [Inject(Required = false)]
    public IGreeter? Optional { get; set; }
}
