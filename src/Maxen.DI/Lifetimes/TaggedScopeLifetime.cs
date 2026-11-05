using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>
/// One instance per labelled scope. Everything created inside the nearest scope carrying the label shares it,
/// even from nested scopes below it. Think "per request" inside a "per tenant" or "per job" inside a "per batch".
/// </summary>
public sealed class TaggedScopeLifetime : Lifetime
{
    private readonly object _tag;

    /// <summary>Creates a lifetime tied to scopes carrying the given label.</summary>
    public TaggedScopeLifetime(object tag) => _tag = tag ?? throw new ArgumentNullException(nameof(tag));

    /// <inheritdoc/>
    public override string Name => $"ScopedTo({_tag})";

    // The instance dies with its labelled scope, so longer-lived services must not hold on to it.
    /// <inheritdoc/>
    public override bool IsScopeBound => true;

    /// <inheritdoc/>
    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        var owner = (context.Scope as Scope)?.FindTagged(_tag);
        if (owner is null)
            throw new ScopeValidationException(
                $"Cannot resolve '{context.Key}': it is scoped to '{_tag}', but no scope labelled '{_tag}' is " +
                $"active. Create one with CreateScope(\"{_tag}\"). Path: {context.DescribePath()}");

        // Build against the owning scope, so the service's own scoped dependencies can't end up in a
        // nested scope that is shorter-lived than the instance.
        context.Scope = owner;

        if (owner.Instances.TryGet(context.Descriptor, out var existing))
            return existing!;

        return owner.Instances.GetOrCreate(context.Descriptor, (context, next), static state => state.next(state.context), context.Descriptor.OwnsInstance);
    }
}
