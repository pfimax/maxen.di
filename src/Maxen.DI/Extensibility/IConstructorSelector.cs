using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>Chooses which constructor the container calls when it builds a type.</summary>
public interface IConstructorSelector
{
    /// <summary>
    /// Picks one constructor. <paramref name="canResolve"/> tells you whether the container can supply
    /// a value for a given parameter.
    /// </summary>
    ConstructorInfo Select(Type implementationType, IReadOnlyList<ConstructorInfo> constructors, Func<ParameterInfo, bool> canResolve);
}
