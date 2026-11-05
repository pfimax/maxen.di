namespace Maxen.DI.Internal;

/// <summary>One step of a <see cref="ResolvePlan"/>: something that can produce a service for a given scope.</summary>
internal abstract class PlanNode
{
    /// <summary>True if this node or anything below it is scoped, which the root container may not build.</summary>
    public bool UsesScoped { get; init; }

    /// <summary>Produces the service, from a cache when its lifetime says so.</summary>
    public abstract object Get(IScope scope);
}
