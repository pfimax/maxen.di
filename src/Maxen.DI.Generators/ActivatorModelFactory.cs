using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Looks at calls such as <c>builder.Register&lt;IFoo, Foo&gt;()</c> and, for the implementation type,
    /// writes out the constructor calls the container would otherwise have to build with expression trees.
    /// </summary>
    internal static class ActivatorModelFactory
    {
        /// <summary>Cheap syntax-only filter: a generic call named Register. The real check needs the semantic model.</summary>
        public static bool IsCandidate(SyntaxNode node)
        {
            if (!(node is InvocationExpressionSyntax invocation)) return false;

            var name = invocation.Expression is MemberAccessExpressionSyntax member ? member.Name : invocation.Expression;
            return name is GenericNameSyntax generic && generic.Identifier.ValueText == "Register";
        }

        public static ActivatorModel? Create(GeneratorSyntaxContext context, CancellationToken token)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            if (!(context.SemanticModel.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol method)) return null;

            // Only our own Register methods count; a Register<T>() from some other library is none of our business.
            if (method.Name != "Register" || method.TypeArguments.Length == 0) return null;
            if (!method.ContainingNamespace.ToDisplayString().StartsWith("Maxen.DI", System.StringComparison.Ordinal)) return null;

            // Register<TImplementation>() and Register<TService, TImplementation>(): the implementation comes last.
            if (!(method.TypeArguments[method.TypeArguments.Length - 1] is INamedTypeSymbol implementation)) return null;

            var compilation = context.SemanticModel.Compilation;
            if (!CanActivate(implementation, compilation)) return null;

            var constructors = implementation.InstanceConstructors
                .Where(c => c.DeclaredAccessibility == Accessibility.Public && CanCall(c, compilation))
                .ToList();
            if (constructors.Count == 0) return null;

            var typeName = Name(implementation);
            var code = new StringBuilder();
            foreach (var constructor in constructors)
            {
                var types = string.Join(", ", constructor.Parameters.Select(p => "typeof(" + Name(p.Type) + ")"));
                var arguments = string.Join(", ", constructor.Parameters.Select((p, i) => "(" + Name(p.Type) + ")args[" + i + "]!"));

                code.Append("            global::Maxen.DI.GeneratedActivators.Add(typeof(").Append(typeName).Append("), ")
                    .Append("new global::System.Type[] { ").Append(types).Append(" }, ")
                    .Append("static args => new ").Append(typeName).Append("(").Append(arguments).AppendLine("));");
            }

            return new ActivatorModel(typeName, code.ToString());
        }

        // Abstract, static, open generic or hidden types can't be created from generated code in this assembly.
        private static bool CanActivate(INamedTypeSymbol type, Compilation compilation)
            => type.TypeKind == TypeKind.Class
               && !type.IsAbstract
               && !type.IsStatic
               && !HasTypeParameter(type)
               && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);

        // ref/out/in, pointers and ref structs can't travel through an object array (the runtime skips these constructors too).
        private static bool CanCall(IMethodSymbol constructor, Compilation compilation)
            => constructor.Parameters.All(p =>
                p.RefKind == RefKind.None
                && p.Type.TypeKind != TypeKind.Pointer
                && p.Type.TypeKind != TypeKind.FunctionPointer
                && p.Type.TypeKind != TypeKind.Dynamic
                && !p.Type.IsRefLikeType
                && !HasTypeParameter(p.Type)
                && IsVisible(p.Type, compilation));

        private static bool IsVisible(ITypeSymbol type, Compilation compilation)
        {
            if (type is IArrayTypeSymbol array) return IsVisible(array.ElementType, compilation);
            if (type is INamedTypeSymbol named && named.IsGenericType && !named.IsDefinition
                && !named.TypeArguments.All(a => IsVisible(a, compilation)))
                return false;
            return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
        }

        private static bool HasTypeParameter(ITypeSymbol type)
        {
            switch (type)
            {
                case ITypeParameterSymbol _:
                    return true;
                case IArrayTypeSymbol array:
                    return HasTypeParameter(array.ElementType);
                case INamedTypeSymbol named:
                    return named.TypeArguments.Any(HasTypeParameter) || (named.ContainingType != null && HasTypeParameter(named.ContainingType));
                default:
                    return false;
            }
        }

        // Fully qualified, so the generated file needs no using directives and can't pick the wrong type.
        private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
