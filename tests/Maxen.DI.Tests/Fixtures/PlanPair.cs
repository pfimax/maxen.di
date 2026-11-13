namespace Maxen.DI.Tests.Fixtures;

/// <summary>Takes the same dependency twice, so nesting it gives a graph where every level is reached by two paths.</summary>
public class PlanPair<T>
{
    public PlanPair(T first, T second)
    {
        First = first;
        Second = second;
    }

    public T First { get; }
    public T Second { get; }
}
