namespace Maxen.DI.Tests.Fixtures;

/// <summary>Breaks the loop with <see cref="LazyLoopA"/> by taking a Lazy.</summary>
public class LazyLoopB
{
    public LazyLoopB(Lazy<LazyLoopA> a) { }
}
