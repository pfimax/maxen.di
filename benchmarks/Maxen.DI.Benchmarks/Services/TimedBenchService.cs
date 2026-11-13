namespace Maxen.DI.Benchmarks.Services;

/// <summary>A decorator around <see cref="IBenchService"/>, as a logging or timing wrapper would be.</summary>
public sealed class TimedBenchService : IBenchService
{
    public TimedBenchService(IBenchService inner) { }
}
