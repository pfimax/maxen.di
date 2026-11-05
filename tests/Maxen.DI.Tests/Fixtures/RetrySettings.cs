namespace Maxen.DI.Tests.Fixtures;

/// <summary>Nested settings, bound from keys like <c>Database:Retry:Count</c>.</summary>
public class RetrySettings
{
    public int Count { get; set; } = 1;
}
