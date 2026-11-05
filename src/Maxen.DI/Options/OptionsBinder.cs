using System.Globalization;
using System.Reflection;

namespace Maxen.DI.Options;

/// <summary>
/// Copies values from flat "Name" / "Section:Name" string pairs onto the properties of an object.
/// Handy for environment variables, command lines and anything shaped like a configuration file.
/// Matching is case-insensitive and nested classes are bound from <c>Parent:Child</c> keys.
/// </summary>
public static class OptionsBinder
{
    /// <summary>Copies values from flat "Section:Name" pairs onto the matching properties of <paramref name="target"/>.</summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Binding reads and writes properties by reflection.")]
    public static void Bind(object target, IReadOnlyDictionary<string, string?> values, string? section = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(values);

        // Make matching case-insensitive without asking callers to build their dictionary a special way.
        var lookup = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        var prefix = string.IsNullOrEmpty(section) ? "" : section + ":";

        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite && !IsNestedObject(property)) continue;

            var key = prefix + property.Name;

            if (lookup.TryGetValue(key, out var text) && text is not null && property.CanWrite)
            {
                property.SetValue(target, ConvertValue(text, property.PropertyType, key));
                continue;
            }

            if (IsNestedObject(property) && lookup.Keys.Any(k => k.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase)))
            {
                // Reuse an existing child object when there is one, so defaults set in the constructor survive.
                var child = property.GetValue(target);
                if (child is null && property.CanWrite)
                {
                    child = Activator.CreateInstance(property.PropertyType)!;
                    property.SetValue(target, child);
                }
                if (child is not null) Bind(child, lookup, key);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Binding reads and writes properties by reflection.")]
    private static bool IsNestedObject(PropertyInfo property)
    {
        var type = property.PropertyType;
        return type.IsClass
            && type != typeof(string)
            && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            && type.GetConstructor(Type.EmptyTypes) is not null;
    }

    private static object? ConvertValue(string text, Type type, string key)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (target == typeof(string)) return text;
            if (target.IsEnum) return Enum.Parse(target, text, ignoreCase: true);
            if (target == typeof(TimeSpan)) return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
            if (target == typeof(Guid)) return Guid.Parse(text);
            if (target == typeof(Uri)) return new Uri(text, UriKind.RelativeOrAbsolute);
            return Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new DIException($"Cannot convert '{text}' to {target.Name} for setting '{key}'.", ex);
        }
    }
}
