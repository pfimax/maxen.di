using Maxen.DI.Pipeline;

namespace Maxen.DI.Extensibility;

/// <summary>What a parameter resolver gets to work with.</summary>
public sealed class ParameterResolutionContext
{
    internal ParameterResolutionContext(ResolveContext context, IReadOnlyDictionary<Type, object>? overrides)
    {
        Context = context;
        Overrides = overrides;
    }

    /// <summary>The service currently being constructed.</summary>
    public ResolveContext Context { get; }

    /// <summary>
    /// Values the framework supplies for specific parameter types, for instance the inner
    /// service handed to a decorator. Null when there are none.
    /// </summary>
    public IReadOnlyDictionary<Type, object>? Overrides { get; }
}
