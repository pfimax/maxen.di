namespace Maxen.DI.Tests.Fixtures;

/// <summary>Node 0 of the plan comparison graph; depends on nodes 1, 2, 3.</summary>
public sealed class ChainNode0 : ChainNode
{
    public ChainNode0(ChainNode1 n1, ChainNode2 n2, ChainNode3 n3) : base(n1, n2, n3) { }
}
