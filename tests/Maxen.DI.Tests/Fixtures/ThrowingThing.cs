namespace Maxen.DI.Tests.Fixtures;

/// <summary>Its constructor always fails, with an exception a test can recognise.</summary>
public class ThrowingThing
{
    public ThrowingThing(string name) => throw new InvalidOperationException("no " + name + " today");
}
