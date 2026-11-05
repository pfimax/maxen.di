using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>Builds one instance per scope and reuses it within that scope.</summary>
internal sealed class ScopedLifetime : Lifetime
{
    public override string Name => "Scoped";
    public override bool IsScopeBound => true;

    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        var scope = context.Scope;

        // Resolving from the root would quietly turn a scoped service into a singleton.
        if (scope.IsRoot && context.Container.Options.ValidateScopes)
            throw new ScopeValidationException(
                $"Cannot resolve scoped service '{context.Key}' from the root container. " +
                $"Create a scope first. Path: {context.DescribePath()}");

        // Already built in this scope? Return it without allocating the creation closure below.
        if (scope.Instances.TryGet(context.Descriptor, out var existing))
            return existing!;

        return scope.Instances.GetOrCreate(context.Descriptor, (context, next), static state => state.next(state.context), context.Descriptor.OwnsInstance);
    }
}
