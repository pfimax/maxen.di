namespace Maxen.DI.Tests.Fixtures;

/// <summary>Takes a value type and a reference type, so both kinds of wrong argument can be tried on it.</summary>
public class TieredThing
{
    public TieredThing(int number, string name)
    {
        Number = number;
        Name = name;
    }

    public int Number { get; }
    public string Name { get; }
}
