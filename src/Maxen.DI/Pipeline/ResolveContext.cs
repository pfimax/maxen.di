using Maxen.DI.Arguments;

namespace Maxen.DI.Pipeline;

/// <summary>
/// Everything known about one service being resolved. Contexts link to their parent, so
/// together they form the dependency path (A needs B needs C).
/// </summary>
public sealed class ResolveContext
{
    internal ResolveContext(
        Container core,
        ServiceKey key,
        ServiceDescriptor descriptor,
        IScope scope,
        ResolveContext? parent,
        ResolveArguments? arguments = null)
    {
        Core = core;
        Key = key;
        Descriptor = descriptor;
        Scope = scope;
        Parent = parent;
        Arguments = arguments;
    }

    internal Container Core { get; }

    /// <summary>Values the caller supplied for this resolve call, or null. Only the requested service sees them.</summary>
    public ResolveArguments? Arguments { get; }

    /// <summary>The service being resolved.</summary>
    public ServiceKey Key { get; }
    /// <summary>The registration that is answering the request.</summary>
    public ServiceDescriptor Descriptor { get; }

    /// <summary>
    /// The scope instances are resolved against. Lifetimes may swap it; singletons switch to the root.
    /// Dependencies resolved through this context inherit whatever is set here.
    /// </summary>
    public IScope Scope { get; set; }

    /// <summary>The service that asked for this one, or null at the top of the chain.</summary>
    public ResolveContext? Parent { get; }

    /// <summary>How many services deep this resolve is; zero at the top.</summary>
    public int Depth
    {
        get
        {
            // Worked out on demand: only diagnostics ask, and every context would otherwise carry an extra field.
            var depth = 0;
            for (var c = Parent; c is not null; c = c.Parent) depth++;
            return depth;
        }
    }
    /// <summary>The container doing the resolving.</summary>
    public IContainer Container => Core;

    /// <summary>Resolves a dependency of the service being built.</summary>
    public object Resolve(ServiceKey key) => Core.ResolveCore(key, Scope, this, required: true)!;

    /// <summary>Resolves a dependency, or returns null if it is not registered.</summary>
    public object? TryResolve(ServiceKey key) => Core.ResolveCore(key, Scope, this, required: false);

    /// <summary>Resolves every registration of a dependency.</summary>
    public IReadOnlyList<object> ResolveAll(ServiceKey key) => Core.ResolveAllCore(key, Scope, this);

    /// <summary>Resolves one specific registration instead of "whatever is registered last".</summary>
    internal object ResolveDescriptor(ServiceDescriptor descriptor, ServiceKey key)
        => Core.Invoke(descriptor, key, Scope, this);

    /// <summary>The dependency chain, outermost first, e.g. <c>A -&gt; B -&gt; C</c>. Used in error messages.</summary>
    public string DescribePath()
    {
        var parts = new List<string>();
        for (var c = this; c is not null; c = c.Parent)
            parts.Add(c.Key.ToString());

        parts.Reverse();
        return string.Join(" -> ", parts);
    }
}
