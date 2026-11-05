using Maxen.DI.Pipeline;
using System.Collections.Concurrent;
using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>Fills in public writable properties marked with <see cref="InjectAttribute"/> after construction.</summary>
public sealed class PropertyInjectionInitializer : IInstanceInitializer
{
    // Reflection over properties is slow-ish, so remember the answer per concrete type.
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _cache = new();

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "UsePropertyInjection is marked RequiresUnreferencedCode, so callers have accepted this.")]
    public void Initialize(object instance, ResolveContext context)
    {
        var properties = _cache.GetOrAdd(instance.GetType(), static type => type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic && p.IsDefined(typeof(InjectAttribute), true))
            .ToArray());

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<InjectAttribute>()!;
            var key = new ServiceKey(property.PropertyType, property.GetCustomAttribute<FromKeyAttribute>()?.Key);

            var value = attribute.Required ? context.Resolve(key) : context.TryResolve(key);
            if (value is not null)
                property.SetValue(instance, value);
        }
    }
}
