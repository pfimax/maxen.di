using Maxen.DI.Internal;
using Maxen.DI.Pipeline;
using System.Collections.Concurrent;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// The fallback resolver. Tries, in order: values supplied by the framework, arguments passed to this
/// resolve call, a registered service (honouring <see cref="FromKeyAttribute"/>), and finally the
/// parameter's own default value.
/// </summary>
public sealed class DefaultParameterResolver : IParameterResolver
{
    /// <inheritdoc/>
    public bool CanResolve(ParameterInfo parameter, ParameterResolutionContext context)
    {
        if (context.Overrides?.ContainsKey(parameter.ParameterType) == true) return true;
        if (context.Context.Arguments?.TryGet(parameter, out _) == true) return true;
        if (context.Context.Container.IsRegistered(KeyOf(parameter))) return true;
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

        // One lookup instead of "is it registered?" followed by "resolve it": TryResolve answers both.
        var resolved = context.Context.TryResolve(KeyOf(parameter));
        return resolved ?? DefaultOf(parameter);
    }

    /// <summary>
    /// The shortcut the factory takes when this is the only resolver and no per-call values are in play:
    /// registered service, else the parameter's default, else a clear error.
    /// </summary>
    internal static object? ResolveDirect(ParameterInfo parameter, ServiceKey key, ResolveContext context)
    {
        var resolved = context.TryResolve(key);
        if (resolved is not null) return resolved;

        if (!parameter.HasDefaultValue)
            throw new ResolutionException(
                $"Cannot resolve parameter '{TypeNames.Format(parameter.ParameterType)} {parameter.Name}'. " +
                $"Path: {context.DescribePath()}");

        return DefaultOf(parameter);
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

    // Reading attributes through reflection allocates every time, and the answer never changes for a
    // given parameter, so we work it out once.
    private static readonly ConcurrentDictionary<ParameterInfo, ServiceKey> Keys = new();

    internal static ServiceKey KeyOf(ParameterInfo parameter)
    {
        if (Keys.TryGetValue(parameter, out var key)) return key;

        key = new ServiceKey(parameter.ParameterType, parameter.GetCustomAttribute<FromKeyAttribute>()?.Key);
        Keys[parameter] = key;
        return key;
    }
}
