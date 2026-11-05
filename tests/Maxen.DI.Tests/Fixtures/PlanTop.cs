namespace Maxen.DI.Tests.Fixtures;

/// <summary>Top of the plan test chain: needs a middle and a second leaf, so lifetimes can be mixed.</summary>
public sealed class PlanTop
{
    public PlanTop(PlanMiddle middle, PlanLeaf leaf)
    {
        Middle = middle;
        Leaf = leaf;
    }

    public PlanMiddle Middle { get; }

    public PlanLeaf Leaf { get; }
}
