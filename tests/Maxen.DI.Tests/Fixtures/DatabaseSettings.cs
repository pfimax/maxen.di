namespace Maxen.DI.Tests.Fixtures;

/// <summary>A settings class used to test options binding.</summary>
public class DatabaseSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public bool UseSsl { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public DatabaseMode Mode { get; set; }
    public RetrySettings Retry { get; set; } = new();
}
