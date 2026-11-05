namespace Maxen.DI.Samples.Generators.Plain;

/// <summary>
/// Registered in code, with no attribute. The generator still sees the Register call and writes a direct constructor
/// call for it, so the container does not need reflection or expression trees to build it.
/// </summary>
public sealed class Plain
{
    public string Hello() => "built by a generated activator";
}
