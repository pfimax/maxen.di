namespace Maxen.DI.Tests.Fixtures;

/// <summary>Node 1 of the plan comparison graph; depends on nodes 3, 4.</summary>
public sealed class ChainNode1 : ChainNode
{
    public ChainNode1(ChainNode3 n3, ChainNode4 n4) : base(n3, n4) { }
}
