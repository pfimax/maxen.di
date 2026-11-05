using System.Reflection;

namespace Maxen.DI.Extensibility;

/// <summary>
/// Produces values for constructor parameters. Resolvers are asked in order and the first one
/// that says it can resolve a parameter wins, so custom resolvers beat the default one.
/// </summary>
public interface IParameterResolver
{
    /// <summary>
    /// Should depend only on registrations and the parameter itself: the answer is used to pick a
    /// constructor and is cached per type.
    /// </summary>
    bool CanResolve(ParameterInfo parameter, ParameterResolutionContext context);

    /// <summary>Produces the value for a parameter this resolver said it can handle.</summary>
    object? Resolve(ParameterInfo parameter, ParameterResolutionContext context);
}
