using System.Reflection;
using Maxen.DI.Extensibility;

namespace Maxen.DI.Samples.Features.Extensibility;

/// <summary>Fills any constructor parameter named "connectionString" with a fixed value.</summary>
internal sealed class ConnectionStringResolver : IParameterResolver
{
    public bool CanResolve(ParameterInfo parameter, ParameterResolutionContext context)
        => parameter.ParameterType == typeof(string) && parameter.Name == "connectionString";

    public object? Resolve(ParameterInfo parameter, ParameterResolutionContext context) => "Server=localhost;Database=demo";
}
