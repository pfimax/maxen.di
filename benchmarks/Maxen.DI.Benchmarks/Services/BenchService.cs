namespace Maxen.DI.Benchmarks.Services;

/// <summary>The top of a small three-level graph, typical of what a request handler pulls in.</summary>
public sealed class BenchService : IBenchService
{
    public BenchService(IBenchRepository repository, IBenchLogger logger) { }
}
