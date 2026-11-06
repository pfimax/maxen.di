using Maxen.DI.Internal;
using Maxen.DI.Pipeline;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// The fallback resolver. Tries, in order: values supplied by the framework, arguments passed to this
/// resolve call, a registered service (honouring <see cref="FromKeyAttribute"/> and Microsoft's <c>[FromKeyedServices]</c>),
/// and finally the parameter's own default value. A parameter marked with Microsoft's <c>[ServiceKey]</c> receives the key
/// of the service being built.
/// </summary>
public sealed class DefaultParameterResolver : IParameterResolver
{
    /// <inheritdoc/>
    public bool CanResolve(ParameterInfo parameter, ParameterResolutionContext context)
    {
        if (context.Overrides?.ContainsKey(parameter.ParameterType) == true) return true;
        if (context.Context.Arguments?.TryGet(parameter, out _) == true) return true;

        var declared = KeyOf(parameter);
        var owner = context.Context.Descriptor.Key.Key;
        if (ParameterKeys.IsOwnKey(declared))
            return ParameterKeys.AcceptsOwnKey(parameter.ParameterType, owner) || parameter.HasDefaultValue;

        var key = ParameterKeys.ForOwner(declared, owner);

        // Only while an any-key registration itself is being inspected: the real key, and so the answer, isn't known yet.
        if (key.IsAnyKey) return true;

        if (context.Context.Container.IsRegistered(key)) return true;
        return parameter.HasDefaultValue;
    }

    /// <inheritdoc/>
    public object? Resolve(ParameterInfo parameter, ParameterResolutionContext context)
    {
        if (context.Overrides is not null && context.Overrides.TryGetValue(parameter.ParameterType, out var supplied))
            return supplied;

        // Values the caller passed for this very call beat anything registered.
        if (context.Context.Arguments is not null && context.Context.Arguments.TryGet(parameter, out var argument))
            return argument;

        var declared = KeyOf(parameter);
        var owner = context.Context.Descriptor.Key.Key;
        if (ParameterKeys.IsOwnKey(declared))
            return ParameterKeys.AcceptsOwnKey(parameter.ParameterType, owner) ? owner : DefaultOf(parameter);

        // One lookup instead of "is it registered?" followed by "resolve it": TryResolve answers both.
        var resolved = context.Context.TryResolve(ParameterKeys.ForOwner(declared, owner));
        return resolved ?? DefaultOf(parameter);
    }

    /// <summary>
    /// The shortcut the factory takes when this is the only resolver and no per-call values are in play:
    /// registered service, else the parameter's default, else a clear error.
    /// </summary>
    internal static object? ResolveDirect(ParameterInfo parameter, ServiceKey key, ResolveContext context)
    {
        // Keys that depend on the registration being built are rare, so one type check is all the common path pays.
        if (key.Key is KeyMarker)
        {
            var owner = context.Descriptor.Key.Key;
            if (ParameterKeys.IsOwnKey(key)) return OwnKeyOf(parameter, owner, context);
            key = ParameterKeys.ForOwner(key, owner);
        }

        var resolved = context.TryResolve(key);
        if (resolved is not null) return resolved;

        if (!parameter.HasDefaultValue)
            throw new ResolutionException(
                $"Cannot resolve parameter '{TypeNames.Format(parameter.ParameterType)} {parameter.Name}'. " +
                $"Path: {context.DescribePath()}");

        return DefaultOf(parameter);
    }

    private static object? OwnKeyOf(ParameterInfo parameter, object? owner, ResolveContext context)
    {
        if (ParameterKeys.AcceptsOwnKey(parameter.ParameterType, owner)) return owner;
        if (parameter.HasDefaultValue) return DefaultOf(parameter);

        var parameterType = TypeNames.Format(parameter.ParameterType);
        var reason = owner is null
            ? "it was registered without a key"
            : $"its key '{owner}' is of type {TypeNames.Format(owner.GetType())}, not {parameterType}";
        throw new ResolutionException(
            $"Parameter '{parameterType} {parameter.Name}' is marked [ServiceKey], but '{context.Key}' cannot supply it: " +
            $"{reason}. Path: {context.DescribePath()}");
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Only value types reach this call, and they always have a default value.")]
    internal static object? DefaultOf(ParameterInfo parameter)
    {
        // For value types the reflection API can hand back null for "default", which would
        // blow up when we unbox it later, so build a real default instead.
        var value = parameter.DefaultValue;
        if (value is null && parameter.ParameterType.IsValueType)
            return Activator.CreateInstance(parameter.ParameterType);

        return value;
    }

    /// <summary>
    /// The key a parameter asks for, as written on it. It can be a marker meaning "the key of the service being built"
    /// or "the key itself", which <see cref="ParameterKeys.ForOwner"/> turns into a real answer.
    /// </summary>
    internal static ServiceKey KeyOf(ParameterInfo parameter) => ParameterKeys.Of(parameter);
}
