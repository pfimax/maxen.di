namespace Maxen.DI.Samples.Features.Extensibility;

internal sealed class Token
{
    private static int _next;

    public int Number { get; } = Interlocked.Increment(ref _next);
}
