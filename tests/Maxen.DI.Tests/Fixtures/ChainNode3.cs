namespace Maxen.DI.Tests.Fixtures;

/// <summary>Node 3 of the plan comparison graph; depends on nodes 5.</summary>
public sealed class ChainNode3 : ChainNode
{
    public ChainNode3(ChainNode5 n5) : base(n5) { }
}
