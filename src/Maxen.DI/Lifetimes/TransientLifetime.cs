using Maxen.DI.Pipeline;

namespace Maxen.DI;

/// <summary>Builds a new instance on every request.</summary>
internal sealed class TransientLifetime : Lifetime
{
    public override string Name => "Transient";

    public override object Resolve(ResolveContext context, ResolveDelegate next)
    {
        var instance = next(context);

        // Nobody else knows about this instance, so the current scope takes over disposal.
        if (context.Descriptor.OwnsInstance)
            context.Scope.Instances.Track(instance);

        return instance;
    }
}
