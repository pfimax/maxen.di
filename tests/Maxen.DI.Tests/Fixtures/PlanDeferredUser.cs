namespace Maxen.DI.Tests.Fixtures;

/// <summary>Takes its leaf through Lazy and Func, so nothing is built until the user asks.</summary>
public sealed class PlanDeferredUser
{
    public PlanDeferredUser(Lazy<PlanLeaf> lazy, Func<PlanLeaf> factory)
    {
        Lazy = lazy;
        Factory = factory;
    }

    public Lazy<PlanLeaf> Lazy { get; }

    public Func<PlanLeaf> Factory { get; }
}
