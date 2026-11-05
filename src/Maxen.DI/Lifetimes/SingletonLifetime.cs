using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>Builds one instance for the whole container.</summary>
internal sealed class SingletonLifetime : Lifetime
{
    public override string Name => "Singleton";

    // A singleton outlives every scope, so it must not hold on to anything scope-bound.
    public override bool CanDependOn(Lifetime dependency) => !dependency.IsScopeBound;

    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        var root = context.Container;

        // Always build against the root so the singleton's own dependencies can never
        // end up tied to whichever scope happened to ask first.
        context.Scope = root;

        // Cached already? Skip the creation closure.
        if (root.Instances.TryGet(context.Descriptor, out var existing))
            return existing!;

        return root.Instances.GetOrCreate(context.Descriptor, (context, next), static state => state.next(state.context), context.Descriptor.OwnsInstance);
    }
}
