namespace Maxen.DI.Tests.Fixtures;

/// <summary>Node 2 of the plan comparison graph; depends on nodes 4, 5.</summary>
public sealed class ChainNode2 : ChainNode
{
    public ChainNode2(ChainNode4 n4, ChainNode5 n5) : base(n4, n5) { }
}
