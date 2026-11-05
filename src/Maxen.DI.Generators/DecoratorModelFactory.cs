using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>Reads a class marked with [Decorator] and works out the <c>builder.Decorate(...)</c> call for it.</summary>
    internal static class DecoratorModelFactory
    {
        public static ServiceModel Create(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;
            var attribute = context.Attributes[0];
            var location = LocationInfo.From(context.TargetNode.GetLocation());
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Sorted after the services so a decorator never comes before the registrations it wraps in the output.
            var sortKey = "~" + typeName;

            if (type.IsAbstract || type.IsStatic)
                return new ServiceModel(sortKey, null, new DiagnosticModel(
                    DiagnosticDescriptors.UnsupportedClass, location, type.Name + "|abstract and static classes are not supported"));

            if (attribute.ConstructorArguments.Length == 0 || !(attribute.ConstructorArguments[0].Value is ITypeSymbol service))
                return new ServiceModel(sortKey, null, new DiagnosticModel(
                    DiagnosticDescriptors.UnsupportedClass, location, type.Name + "|the service type must be written as typeof(...)"));

            // An open generic decorator (LoggingRepo<T>) pairs with an open generic service typeof(IRepo<>); closed with closed.
            var open = type.IsGenericType;
            var unbound = service is INamedTypeSymbol serviceName && serviceName.IsUnboundGenericType;
            if (open != unbound)
                return new ServiceModel(sortKey, null, new DiagnosticModel(
                    DiagnosticDescriptors.UnsupportedClass, location,
                    type.Name + "|a generic decorator needs an open service such as typeof(IRepo<>), and a plain one a closed service"));

            if (open ? !ImplementsOpen(type, (INamedTypeSymbol)service) : !ServiceModelFactory.Implements(type, service))
                return new ServiceModel(sortKey, null, new DiagnosticModel(
                    DiagnosticDescriptors.NotAssignable, location, type.Name + "|" + service.Name));

            var priority = 0;
            foreach (var named in attribute.NamedArguments)
                if (named.Key == "Priority" && named.Value.Value is int value) priority = value;

            // typeof(LoggingRepo<>) is written without type arguments, which is what ConstructUnboundGenericType displays.
            var decoratorName = open
                ? type.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : typeName;
            var statement = "builder.Decorate(typeof(" + service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) +
                "), typeof(" + decoratorName + "), null, " + priority + ");";

            // Still registered (the runtime will explain itself too), but the mistake is pointed out at compile time.
            DiagnosticModel? problem = null;
            if (!TakesService(type, service))
                problem = new DiagnosticModel(DiagnosticDescriptors.DecoratorWithoutInner, location, type.Name + "|" + service.Name);

            return new ServiceModel(sortKey, statement, problem);
        }

        // A decorator wraps the service it receives, so some constructor must accept one.
        private static bool TakesService(INamedTypeSymbol decorator, ITypeSymbol service)
        {
            foreach (var constructor in decorator.InstanceConstructors)
                foreach (var parameter in constructor.Parameters)
                    if (SymbolEqualityComparer.Default.Equals(parameter.Type, service)
                        || (parameter.Type is INamedTypeSymbol parameterType && service is INamedTypeSymbol wanted && wanted.IsUnboundGenericType
                            && SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, wanted.OriginalDefinition)))
                        return true;
            return false;
        }

        // LoggingRepo<T> implements IRepo<T>: compare the generic definitions, not the constructed types.
        private static bool ImplementsOpen(INamedTypeSymbol type, INamedTypeSymbol service)
        {
            var wanted = service.OriginalDefinition;
            if (service.TypeKind == TypeKind.Interface)
                return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, wanted));

            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                if (SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, wanted)) return true;
            return false;
        }
    }
}
