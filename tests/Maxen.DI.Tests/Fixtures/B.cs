namespace Maxen.DI.Tests.Fixtures;

/// <summary>The other half of the dependency loop with <see cref="A"/>.</summary>
public class B
{
    public B(A a) { }
}
