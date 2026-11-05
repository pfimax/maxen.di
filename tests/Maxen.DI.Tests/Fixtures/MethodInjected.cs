namespace Maxen.DI.Tests.Fixtures;

/// <summary>Gets its clock through an [Inject] method instead of the constructor.</summary>
public class MethodInjected
{
    public IClock? Clock { get; private set; }
    public int Calls { get; private set; }

    [Inject]
    public void Setup(IClock clock)
    {
        Clock = clock;
        Calls++;
    }

    // Not marked, so the container must leave it alone.
    public void NotInjected(IClock clock) => throw new InvalidOperationException("should never be called");
}
