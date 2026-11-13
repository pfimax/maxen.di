namespace Maxen.DI.Tests.Fixtures;

/// <summary>The second way to <see cref="PlanSharedMiddle"/>.</summary>
public class PlanSharedHolder
{
    public PlanSharedHolder(PlanSharedMiddle middle) => Middle = middle;

    public PlanSharedMiddle Middle { get; }
}
