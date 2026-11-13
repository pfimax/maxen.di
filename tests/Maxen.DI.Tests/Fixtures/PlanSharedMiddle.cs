namespace Maxen.DI.Tests.Fixtures;

/// <summary>The service two paths share; it needs a <see cref="PlanPairLeaf"/>.</summary>
public class PlanSharedMiddle
{
    public PlanSharedMiddle(PlanPairLeaf leaf) => Leaf = leaf;

    public PlanPairLeaf Leaf { get; }
}
