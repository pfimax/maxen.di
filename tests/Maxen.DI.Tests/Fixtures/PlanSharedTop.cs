namespace Maxen.DI.Tests.Fixtures;

/// <summary>Reaches <see cref="PlanSharedMiddle"/> twice: directly first, then through <see cref="PlanSharedHolder"/>.</summary>
public class PlanSharedTop
{
    public PlanSharedTop(PlanSharedMiddle middle, PlanSharedHolder holder)
    {
        Middle = middle;
        Holder = holder;
    }

    public PlanSharedMiddle Middle { get; }
    public PlanSharedHolder Holder { get; }
}
