namespace Maxen.DI.Tests.Fixtures;

/// <summary>A second IGreeter implementation, used to test multiple registrations.</summary>
public sealed class QuietGreeter : IGreeter
{
    public string Greet() => "psst";
}
