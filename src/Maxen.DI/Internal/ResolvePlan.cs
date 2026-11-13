namespace Maxen.DI.Internal;

/// <summary>
/// A ready-made recipe for resolving one plain registration from the top: the whole dependency tree is worked out
/// once, so later resolves build objects without walking the pipeline for every dependency. It only exists when
/// nothing could change the outcome (no middleware, decorators, conditions or factories), and anything unusual
/// makes the planner give up, so the normal path runs and reports any error in its usual words.
/// </summary>
internal sealed class ResolvePlan
{
    /// <summary>Stored on a descriptor that cannot be planned, so the question is only asked once.</summary>
    public static readonly ResolvePlan None = new(null);

    private readonly PlanNode? _root;

    public ResolvePlan(PlanNode? root, int nodeCount = 0)
    {
        _root = root;
        NodeCount = nodeCount;
    }

    /// <summary>How many steps the recipe holds. A service many others share is one step, however many paths lead to it.</summary>
    public int NodeCount { get; }

    public bool IsUsable => _root is not null;

    /// <summary>
    /// Builds or fetches the service. Returns false when the plan must not be used for this call, so the caller falls
    /// back to the full pipeline (which is where the "needs a scope" error comes from).
    /// </summary>
    public bool TryRun(IScope scope, bool validateScopes, out object? result)
    {
        if (_root!.UsesScoped && validateScopes && scope.IsRoot)
        {
            result = null;
            return false;
        }

        result = _root.Get(scope);
        return true;
    }
}
