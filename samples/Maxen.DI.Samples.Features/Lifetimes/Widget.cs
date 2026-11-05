namespace Maxen.DI.Samples.Features.Lifetimes;

/// <summary>Numbered on creation, so the output shows whether two resolves got the same object.</summary>
internal sealed class Widget
{
    private static int _next;

    public int Id { get; } = Interlocked.Increment(ref _next);
}
