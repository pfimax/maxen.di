using System.Reflection;

namespace Maxen.DI;

/// <summary>One shelf slot: a constructor, identified by its parameter types, and the generated call that invokes it.</summary>
internal sealed class GeneratedActivatorEntry
{
    private readonly Type[] _parameterTypes;

    public GeneratedActivatorEntry(Type[] parameterTypes, Func<object?[], object> create)
    {
        _parameterTypes = parameterTypes;
        Create = create;
    }

    public Func<object?[], object> Create { get; }

    public bool Matches(Type[] types) => types.AsSpan().SequenceEqual(_parameterTypes);

    public bool Matches(ParameterInfo[] parameters)
    {
        if (parameters.Length != _parameterTypes.Length) return false;
        for (var i = 0; i < parameters.Length; i++)
            if (parameters[i].ParameterType != _parameterTypes[i]) return false;
        return true;
    }
}
