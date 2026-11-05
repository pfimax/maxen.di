namespace Maxen.DI.Tests.Fixtures;

/// <summary>Needs B, which needs A: a deliberate dependency loop.</summary>
public class A
{
    public A(B b) { }
}
