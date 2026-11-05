namespace Maxen.DI.Tests.Fixtures;

/// <summary>Node 4 of the plan comparison graph; depends on nodes 5.</summary>
public sealed class ChainNode4 : ChainNode
{
    public ChainNode4(ChainNode5 n5) : base(n5) { }
}
