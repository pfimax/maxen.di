namespace Maxen.DI.Tests.Fixtures;

/// <summary>Needs B directly, while B only holds a Lazy of A, so the loop is legal.</summary>
public class LazyLoopA
{
    public LazyLoopA(LazyLoopB b) { }
}
