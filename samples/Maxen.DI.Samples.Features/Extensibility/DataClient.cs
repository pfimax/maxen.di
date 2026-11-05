namespace Maxen.DI.Samples.Features.Extensibility;

internal sealed class DataClient
{
    public DataClient(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public DateTime? Stamped { get; set; }
}
