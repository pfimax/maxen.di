namespace Maxen.DI.Tests.Fixtures;

/// <summary>Bottom of the plan test chain. Counts how often it is created and disposed.</summary>
public sealed class PlanLeaf : IDisposable
{
    public static int Created;
    public static int Disposed;

    public PlanLeaf() => Interlocked.Increment(ref Created);

    public void Dispose() => Interlocked.Increment(ref Disposed);
}
