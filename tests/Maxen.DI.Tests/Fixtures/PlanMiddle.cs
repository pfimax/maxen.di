namespace Maxen.DI.Tests.Fixtures;

/// <summary>Middle of the plan test chain; the greeting has a default so the plan has a non-service argument to handle.</summary>
public sealed class PlanMiddle
{
    public PlanMiddle(PlanLeaf leaf, string greeting = "hello")
    {
        Leaf = leaf;
        Greeting = greeting;
    }

    public PlanLeaf Leaf { get; }

    public string Greeting { get; }
}
