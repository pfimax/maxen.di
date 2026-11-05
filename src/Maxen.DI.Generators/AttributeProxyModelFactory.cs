using System.Threading;
using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Finds the interface a class registers as through [Service(ServiceType = typeof(IFoo))] and builds its proxy, so a class
    /// marked with [Intercept] gets its interception class at compile time too.
    /// </summary>
    internal static class AttributeProxyModelFactory
    {
        public static ProxyModel? Create(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;

            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != "Maxen.DI.ServiceAttribute") continue;

                foreach (var named in attribute.NamedArguments)
                    if (named.Key == "ServiceType" && named.Value.Value is INamedTypeSymbol service)
                        return ProxyModelFactory.ForService(service, context.SemanticModel.Compilation);
            }

            // Without a ServiceType the service is the class itself, which cannot be proxied (the model factory says so).
            return null;
        }
    }
}
