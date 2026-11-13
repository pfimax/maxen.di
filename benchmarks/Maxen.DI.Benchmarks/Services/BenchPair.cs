namespace Maxen.DI.Benchmarks.Services;

/// <summary>
/// Takes the same dependency twice. Nested (a pair of pairs of pairs...), it gives a graph in which every level is
/// reached by two paths, the shape of an application where many services share the ones below them.
/// </summary>
public sealed class BenchPair<T>
{
    public BenchPair(T first, T second) { }
}
