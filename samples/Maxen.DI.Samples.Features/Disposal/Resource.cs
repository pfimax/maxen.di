namespace Maxen.DI.Samples.Features.Disposal;

internal sealed class Resource : IDisposable
{
    private static int _next;
    private readonly int _id = Interlocked.Increment(ref _next);

    public void Dispose() => Console.WriteLine($"  resource #{_id} disposed");
}
