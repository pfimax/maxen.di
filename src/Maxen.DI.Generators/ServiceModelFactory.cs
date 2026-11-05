using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>Reads a class marked with [Service] and works out the code needed to register it.</summary>
    internal static class ServiceModelFactory
    {
        private const string InjectAttribute = "Maxen.DI.InjectAttribute";
        private const string FromKeyAttribute = "Maxen.DI.FromKeyAttribute";

        public static ServiceModel Create(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var type = (INamedTypeSymbol)context.TargetSymbol;
            var attribute = context.Attributes[0];
            var location = LocationInfo.From(context.TargetNode.GetLocation());
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // MAXEN002's message names the class and then the reason ("'{0}' cannot be registered ({1})").
            ServiceModel Fail(DiagnosticDescriptor descriptor, string argument)
                => new ServiceModel(typeName, null, new DiagnosticModel(
                    descriptor, location, ReferenceEquals(descriptor, DiagnosticDescriptors.UnsupportedClass) ? type.Name + "|" + argument : argument));

            if (type.IsAbstract || type.IsStatic || type.IsGenericType)
                return Fail(DiagnosticDescriptors.UnsupportedClass, "abstract, static and generic classes are not supported");

            ReadAttribute(attribute, out var lifetime, out var serviceType, out var key);
            var service = serviceType ?? type;

            if (!Implements(type, service))
                return new ServiceModel(typeName, null, new DiagnosticModel(
                    DiagnosticDescriptors.NotAssignable, location, type.Name + "|" + service.Name));

            var constructor = ChooseConstructor(type, out var ambiguous);
            if (ambiguous)
                return Fail(DiagnosticDescriptors.AmbiguousConstructor, type.Name);

            if (constructor is null)
                return Fail(DiagnosticDescriptors.UnsupportedClass, "it has no accessible constructor");

            var arguments = new List<string>();
            var dependencies = new List<string>();
            foreach (var parameter in constructor.Parameters)
            {
                // Defaults and ref/out can't be expressed as a plain "resolve it" call.
                if (parameter.HasExplicitDefaultValue || parameter.RefKind != RefKind.None)
                    return Fail(DiagnosticDescriptors.UnsupportedClass, "parameter '" + parameter.Name + "' is optional or by-reference");

                arguments.Add(ResolveExpression(parameter));
                dependencies.Add(parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "|" + KeyOf(parameter));
            }

            var expression =
                "builder.RegisterFactory(typeof(" + service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "), ctx => new " +
                typeName + "(" + string.Join(", ", arguments) + "))" +
                ".WithLifetime(global::Maxen.DI.Lifetime." + lifetime + ")" +
                (key is null ? "" : ".WithKey(" + key + ")");

            // [Intercept(typeof(X))] becomes InterceptWith(..., new X()), written as a plain static call because the
            // generated file has no using directives. Calls are made lowest Order first, the same as RegisterAttributed does.
            var interceptors = InterceptorsOf(type);
            if (interceptors.Count > 0)
            {
                if (service.TypeKind != TypeKind.Interface)
                    return Fail(DiagnosticDescriptors.UnsupportedClass, "[Intercept] needs the service type to be an interface");

                foreach (var interceptor in interceptors)
                {
                    if (!HasPublicParameterlessConstructor(interceptor))
                        return Fail(DiagnosticDescriptors.UnsupportedClass,
                            "interceptor '" + interceptor.Name + "' needs a public parameterless constructor");

                    expression = "global::Maxen.DI.Interception.InterceptionExtensions.InterceptWith(" + expression + ", new " +
                        interceptor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "())";
                }
            }
            var statement = expression + ";";

            var info = new ServiceInfo(
                typeName, service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), key, lifetime,
                string.Join(";", dependencies), location);

            return new ServiceModel(typeName, statement, null, info);
        }

        // The [Intercept] interceptor types of a class, lowest Order first; equal orders keep their written order.
        private static List<INamedTypeSymbol> InterceptorsOf(INamedTypeSymbol type)
        {
            var found = new List<KeyValuePair<int, INamedTypeSymbol>>();
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != "Maxen.DI.InterceptAttribute") continue;
                if (attribute.ConstructorArguments.Length == 0 || !(attribute.ConstructorArguments[0].Value is INamedTypeSymbol interceptor)) continue;

                var order = 0;
                foreach (var named in attribute.NamedArguments)
                    if (named.Key == "Order" && named.Value.Value is int value) order = value;
                found.Add(new KeyValuePair<int, INamedTypeSymbol>(order, interceptor));
            }

            // OrderBy is stable. The first InterceptWith call is the outermost interceptor, which matches "lowest Order first".
            return found.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        }

        private static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
            => !type.IsAbstract && type.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0);

        /// <summary>The [FromKey] value of a parameter as C# source, or an empty string when it has none.</summary>
        private static string KeyOf(IParameterSymbol parameter)
        {
            var fromKey = parameter.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == FromKeyAttribute);
            return fromKey is not null && fromKey.ConstructorArguments.Length > 0
                ? ConstantFormatter.Format(fromKey.ConstructorArguments[0])
                : "";
        }

        private static void ReadAttribute(AttributeData attribute, out string lifetime, out ITypeSymbol? serviceType, out string? key)
        {
            // The constructor argument is the ServiceLifetime enum: 0 = Transient, 1 = Scoped, 2 = Singleton.
            var raw = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as int? : null;
            lifetime = raw == 1 ? "Scoped" : raw == 2 ? "Singleton" : "Transient";

            serviceType = null;
            key = null;
            foreach (var named in attribute.NamedArguments)
            {
                if (named.Key == "ServiceType" && named.Value.Value is ITypeSymbol symbol)
                    serviceType = symbol;
                else if (named.Key == "Key" && !named.Value.IsNull)
                    key = ConstantFormatter.Format(named.Value);
            }
        }

        internal static bool Implements(INamedTypeSymbol type, ITypeSymbol service)
        {
            if (SymbolEqualityComparer.Default.Equals(type, service)) return true;

            if (service.TypeKind == TypeKind.Interface)
                return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, service));

            for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
                if (SymbolEqualityComparer.Default.Equals(baseType, service)) return true;

            return false;
        }

        /// <summary>
        /// Mirrors the runtime rule as far as it can be known at compile time: [Inject] wins, otherwise the
        /// constructor with the most parameters. (At runtime the greediest one the container can satisfy is chosen.)
        /// </summary>
        private static IMethodSymbol? ChooseConstructor(INamedTypeSymbol type, out bool ambiguous)
        {
            ambiguous = false;

            var candidates = type.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public
                         || c.DeclaredAccessibility == Accessibility.Internal
                         || c.DeclaredAccessibility == Accessibility.ProtectedOrInternal)
                .ToList();
            if (candidates.Count == 0) return null;

            var marked = candidates.Where(c => c.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == InjectAttribute)).ToList();
            var pool = marked.Count > 0 ? marked : candidates;

            var most = pool.Max(c => c.Parameters.Length);
            var best = pool.Where(c => c.Parameters.Length == most).ToList();
            if (best.Count > 1)
            {
                ambiguous = true;
                return null;
            }
            return best[0];
        }

        private static string ResolveExpression(IParameterSymbol parameter)
        {
            var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var fromKey = parameter.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == FromKeyAttribute);

            var key = fromKey is not null && fromKey.ConstructorArguments.Length > 0
                ? ", " + ConstantFormatter.Format(fromKey.ConstructorArguments[0])
                : "";

            // A plain static call, so the generated file needs no using directives.
            return "global::Maxen.DI.ScopeExtensions.Resolve<" + typeName + ">(ctx" + key + ")";
        }
    }
}
