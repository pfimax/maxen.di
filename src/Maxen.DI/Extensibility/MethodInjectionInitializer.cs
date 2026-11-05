using Maxen.DI.Pipeline;
using System.Collections.Concurrent;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// After construction, calls every public instance method marked with <see cref="InjectAttribute"/>,
/// handing it the services it asks for. Useful when a constructor would get unwieldy, or for
/// optional extras that shouldn't be forced on every caller.
/// </summary>
public sealed class MethodInjectionInitializer : IInstanceInitializer
{
    // Reflection over methods is slow-ish, so remember the answer per concrete type.
    private readonly ConcurrentDictionary<Type, MethodInfo[]> _cache = new();

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "UseMethodInjection is marked RequiresUnreferencedCode, so callers have accepted this.")]
    public void Initialize(object instance, ResolveContext context)
    {
        var methods = _cache.GetOrAdd(instance.GetType(), static type => type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsGenericMethodDefinition && m.IsDefined(typeof(InjectAttribute), true))
            .ToArray());

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            var arguments = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
                arguments[i] = DefaultParameterResolver.ResolveDirect(parameters[i], DefaultParameterResolver.KeyOf(parameters[i]), context);

            try
            {
                method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // The caller cares about what their method threw, not about the reflection wrapper around it.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}
