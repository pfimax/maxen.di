using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Finds the interfaces you intercept in code and writes a proxy class for each, so interception needs
    /// no runtime code generation. Interfaces with anything the generator can't forward (generic methods,
    /// ref parameters, events, indexers) are skipped and fall back to the runtime proxy.
    /// </summary>
    internal static class ProxyModelFactory
    {
        private static readonly HashSet<string> RegisterCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "Register", "RegisterFactory", "RegisterInstance", "Replace", "ReplaceFactory", "ReplaceInstance", "Override",
        };

        // Types in generated signatures are fully qualified, with nullable "?" left out because generated code is not nullable-aware.
        private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

        /// <summary>Cheap syntax-only filter: a call named Intercept or InterceptWith (generic or not).</summary>
        public static bool IsCandidate(SyntaxNode node)
        {
            if (!(node is InvocationExpressionSyntax invocation)) return false;

            var name = invocation.Expression is MemberAccessExpressionSyntax member ? member.Name : invocation.Expression;
            var identifier = name is GenericNameSyntax generic ? generic.Identifier.ValueText
                : name is IdentifierNameSyntax simple ? simple.Identifier.ValueText
                : null;
            return identifier == "Intercept" || identifier == "InterceptWith";
        }

        public static ProxyModel? Create(GeneratorSyntaxContext context, CancellationToken token)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var model = context.SemanticModel;
            if (!(model.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol method)) return null;
            if (!InMaxen(method)) return null;

            INamedTypeSymbol? service = null;
            if (method.Name == "Intercept" && method.TypeArguments.Length >= 1)
                service = method.TypeArguments[0] as INamedTypeSymbol;
            else if (method.Name == "InterceptWith")
                service = FindRegisteredService(invocation, model, token);

            if (service == null || service.TypeKind != TypeKind.Interface) return null;

            return ForService(service, model.Compilation);
        }

        /// <summary>The proxy for one interface, or null if it isn't accessible or has members the generator can't forward.</summary>
        public static ProxyModel? ForService(INamedTypeSymbol service, Compilation compilation)
        {
            if (service.TypeKind != TypeKind.Interface) return null;
            if (!compilation.IsSymbolAccessibleWithin(service, compilation.Assembly)) return null;

            var body = BuildClass(service, compilation);
            return body == null ? null : new ProxyModel(Name(service), Name(service), body);
        }

        private static bool InMaxen(IMethodSymbol method)
            => method.ContainingNamespace.ToDisplayString().StartsWith("Maxen.DI", StringComparison.Ordinal);

        // builder.Register<IFoo, Foo>().AsSingleton().InterceptWith<X>(): walk back along the chain to find Register<IFoo, ...>.
        private static INamedTypeSymbol? FindRegisteredService(
            InvocationExpressionSyntax invocation, SemanticModel model, CancellationToken token)
        {
            var current = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
            while (current is InvocationExpressionSyntax call)
            {
                if (model.GetSymbolInfo(call, token).Symbol is IMethodSymbol method
                    && InMaxen(method) && RegisterCalls.Contains(method.Name) && method.TypeArguments.Length >= 1)
                    return method.TypeArguments[0] as INamedTypeSymbol;

                current = (call.Expression as MemberAccessExpressionSyntax)?.Expression;
            }
            return null;
        }

        // Returns the class source, or null when the interface has something we can't forward.
        private static string? BuildClass(INamedTypeSymbol service, Compilation compilation)
        {
            // MethodInfo fields come first, then the members that use them.
            var fields = new StringBuilder();
            var members = new StringBuilder();
            var index = 0;

            foreach (var declaring in new[] { service }.Concat(service.AllInterfaces))
            {
                foreach (var member in declaring.GetMembers())
                {
                    // Default interface methods have a body already; calls to them just aren't intercepted.
                    if (!member.IsAbstract) continue;
                    if (member.IsStatic) return null;

                    switch (member)
                    {
                        case IMethodSymbol m when m.MethodKind == MethodKind.Ordinary:
                            if (!CanForward(m, compilation)) return null;
                            AppendMethod(fields, members, declaring, m, ref index);
                            break;
                        case IPropertySymbol p:
                            if (p.IsIndexer || !CanForwardProperty(p, compilation)) return null;
                            AppendProperty(fields, members, declaring, p, ref index);
                            break;
                        case IMethodSymbol _:
                            // Property accessors are written together with their property.
                            break;
                        default:
                            // Events and anything else we don't know how to forward.
                            return null;
                    }
                }
            }

            var code = new StringBuilder();
            code.AppendLine("    internal sealed class {CLASS} : global::Maxen.DI.Interception.GeneratedProxyBase, " + Name(service));
            code.AppendLine("    {");
            code.AppendLine("        public {CLASS}(object target, global::System.Collections.Generic.IReadOnlyList<global::Maxen.DI.Interception.IInterceptor> interceptors)");
            code.AppendLine("            : base(target, interceptors) { }");
            code.AppendLine();
            code.Append(fields);
            code.Append(members);
            code.AppendLine("    }");
            return code.ToString();
        }

        private static bool CanForward(IMethodSymbol method, Compilation compilation)
        {
            if (method.IsGenericMethod || method.ReturnsByRef || method.ReturnsByRefReadonly) return false;
            if (!method.ReturnsVoid && !IsUsable(method.ReturnType, compilation)) return false;
            return method.Parameters.All(p => p.RefKind == RefKind.None && IsUsable(p.Type, compilation));
        }

        private static bool CanForwardProperty(IPropertySymbol property, Compilation compilation)
            => !property.ReturnsByRef && !property.ReturnsByRefReadonly && IsUsable(property.Type, compilation);

        // Pointers, ref structs, dynamic and hidden types can't go through object arguments or generated code.
        private static bool IsUsable(ITypeSymbol type, Compilation compilation)
        {
            if (type.TypeKind == TypeKind.Pointer || type.TypeKind == TypeKind.FunctionPointer
                || type.TypeKind == TypeKind.Dynamic || type.IsRefLikeType || type.TypeKind == TypeKind.TypeParameter)
                return false;

            if (type is IArrayTypeSymbol array) return IsUsable(array.ElementType, compilation);
            if (type is INamedTypeSymbol named && named.IsGenericType && !named.TypeArguments.All(a => IsUsable(a, compilation)))
                return false;

            return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
        }

        private static void AppendMethod(
            StringBuilder fields, StringBuilder members, INamedTypeSymbol declaring, IMethodSymbol method, ref int index)
        {
            var declaringName = Name(declaring);
            var id = index++;
            var parameters = string.Join(", ", method.Parameters.Select(p => Name(p.Type) + " " + Escape(p.Name)));
            var types = string.Join(", ", method.Parameters.Select(p => "typeof(" + Name(p.Type) + ")"));
            var forwarded = string.Join(", ", method.Parameters.Select((p, i) => "(" + Name(p.Type) + ")a[" + i + "]"));
            var passed = method.Parameters.Length == 0
                ? "global::System.Array.Empty<object>()"
                : "new object[] { " + string.Join(", ", method.Parameters.Select(p => Escape(p.Name))) + " }";

            fields.AppendLine("        private static readonly global::System.Reflection.MethodInfo M" + id + " = typeof(" + declaringName
                + ").GetMethod(\"" + method.Name + "\", new global::System.Type[] { " + types + " })!;");

            var returnType = method.ReturnsVoid ? "void" : Name(method.ReturnType);
            var call = "((" + declaringName + ")t)." + method.Name + "(" + forwarded + ")";

            members.AppendLine();
            members.AppendLine("        " + returnType + " " + declaringName + "." + method.Name + "(" + parameters + ")");
            members.AppendLine("        {");
            members.AppendLine(method.ReturnsVoid
                ? "            Call(M" + id + ", " + passed + ", static (t, a) => { " + call + "; return null; });"
                : "            return (" + returnType + ")Call(M" + id + ", " + passed + ", static (t, a) => " + call + ");");
            members.AppendLine("        }");
        }

        private static void AppendProperty(
            StringBuilder fields, StringBuilder members, INamedTypeSymbol declaring, IPropertySymbol property, ref int index)
        {
            var declaringName = Name(declaring);
            var typeName = Name(property.Type);

            members.AppendLine();
            members.AppendLine("        " + typeName + " " + declaringName + "." + property.Name);
            members.AppendLine("        {");

            if (property.GetMethod != null)
            {
                var id = index++;
                fields.AppendLine("        private static readonly global::System.Reflection.MethodInfo M" + id + " = typeof(" + declaringName
                    + ").GetProperty(\"" + property.Name + "\")!.GetMethod!;");
                members.AppendLine("            get => (" + typeName + ")Call(M" + id
                    + ", global::System.Array.Empty<object>(), static (t, a) => ((" + declaringName + ")t)." + property.Name + ");");
            }

            if (property.SetMethod != null)
            {
                var id = index++;
                fields.AppendLine("        private static readonly global::System.Reflection.MethodInfo M" + id + " = typeof(" + declaringName
                    + ").GetProperty(\"" + property.Name + "\")!.SetMethod!;");
                members.AppendLine("            set => Call(M" + id + ", new object[] { value }, static (t, a) => { ((" + declaringName + ")t)."
                    + property.Name + " = (" + typeName + ")a[0]; return null; });");
            }

            members.AppendLine("        }");
        }

        private static string Name(ITypeSymbol type) => type.ToDisplayString(TypeFormat);

        // Parameter names can be C# keywords (@event, @object), so always escape.
        private static string Escape(string name) => "@" + name;
    }
}
