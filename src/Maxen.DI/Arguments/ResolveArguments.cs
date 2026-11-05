using System.Reflection;

namespace Maxen.DI.Arguments;

/// <summary>
/// Values you hand to the container for one resolve call, for the constructor parameters that
/// can't come from registrations (an id, a name, a request). Only the service you ask for
/// receives them, never its dependencies.
/// </summary>
public sealed class ResolveArguments
{
    private readonly List<ResolveArgumentEntry> _entries = new();

    /// <summary>Supplies a value for the parameter with this name (matched ignoring case).</summary>
    public ResolveArguments Add(string name, object? value)
    {
        _entries.Add(new ResolveArgumentEntry(name ?? throw new ArgumentNullException(nameof(name)), null, value));
        return this;
    }

    /// <summary>Supplies a value for parameters of this type. With several values of one type, order decides.</summary>
    public ResolveArguments Add(Type type, object? value)
    {
        _entries.Add(new ResolveArgumentEntry(null, type ?? throw new ArgumentNullException(nameof(type)), value));
        return this;
    }

    /// <summary>Adds a value that is matched to a constructor parameter by its type.</summary>
    public ResolveArguments Add<T>(T value) => Add(typeof(T), value);

    /// <summary>
    /// Builds arguments from the properties of an object, usually an anonymous one:
    /// <c>ResolveArguments.FromObject(new { name = "Ada", retries = 3 })</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the properties of the object by reflection.")]
    public static ResolveArguments FromObject(object values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var arguments = new ResolveArguments();
        foreach (var property in values.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            arguments.Add(property.Name, property.GetValue(values));
        return arguments;
    }

    internal bool TryGet(ParameterInfo parameter, out object? value)
    {
        // Names are the most specific way to ask, so they take priority over types.
        foreach (var entry in _entries)
        {
            if (entry.Name is null || !string.Equals(entry.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Accepts(parameter.ParameterType, entry.Value)) continue;

            value = entry.Value;
            return true;
        }

        // Typed values are matched by position among the parameters of that type, so
        // Func<int, int, T> hands the first int to the first int parameter and the second to the second.
        var siblings = parameter.Member is MethodBase method ? method.GetParameters() : Array.Empty<ParameterInfo>();
        var index = siblings.Take(parameter.Position).Count(p => p.ParameterType == parameter.ParameterType);

        var typed = _entries.Where(e => e.Type is not null && e.Type == parameter.ParameterType).ToList();
        if (typed.Count == 0)
            typed = _entries.Where(e => e.Type is not null && parameter.ParameterType.IsAssignableFrom(e.Type)).ToList();

        if (index < typed.Count && Accepts(parameter.ParameterType, typed[index].Value))
        {
            value = typed[index].Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool Accepts(Type parameterType, object? value)
        => value is null
            ? !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) is not null
            : parameterType.IsInstanceOfType(value);
}
