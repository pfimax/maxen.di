using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Maxen.DI;

/// <summary>
/// A shelf of ready-made constructor calls. The source generator fills it at startup with a plain
/// <c>new Foo(...)</c> for every type your code registers, so the container can skip building (and
/// compiling) an expression tree for that constructor. Nothing here is required: a type that isn't on
/// the shelf is simply handled the usual way.
/// </summary>
public static class GeneratedActivators
{
    private static readonly ConcurrentDictionary<Type, List<GeneratedActivatorEntry>> Shelf = new();

    /// <summary>
    /// With native AOT, compiled expression trees fall back to a slow interpreter, so the generated calls win even for the
    /// fast construction path (at the price of one argument array per object). On a normal runtime the
    /// compiled path stays in charge because it needs no array. Settable so tests and special hosts can choose.
    /// </summary>
    public static bool PreferForAllConstruction { get; set; } = !RuntimeFeature.IsDynamicCodeCompiled;

    /// <summary>
    /// Adds the constructor call for one constructor. Called from generated code; the parameter types
    /// identify which constructor it is, because a type can have several.
    /// </summary>
    public static void Add(Type type, Type[] parameterTypes, Func<object?[], object> create)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(create);

        var list = Shelf.GetOrAdd(type, static _ => new List<GeneratedActivatorEntry>());
        lock (list)
        {
            // Registering the same constructor twice (say, from two assemblies) keeps the first, which is equivalent.
            if (!list.Any(e => e.Matches(parameterTypes)))
                list.Add(new GeneratedActivatorEntry(parameterTypes, create));
        }
    }

    /// <summary>Finds the generated call for a constructor, if the generator saw it.</summary>
    internal static bool TryGet(ConstructorInfo constructor, ParameterInfo[] parameters, out Func<object?[], object> create)
    {
        create = null!;
        if (constructor.DeclaringType is not { } type || !Shelf.TryGetValue(type, out var list)) return false;

        lock (list)
        {
            foreach (var entry in list)
            {
                if (entry.Matches(parameters))
                {
                    create = entry.Create;
                    return true;
                }
            }
        }
        return false;
    }
}
