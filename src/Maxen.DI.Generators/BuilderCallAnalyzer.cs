using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Reads the registrations you write in code (<c>builder.Register&lt;A&gt;().AsSingleton()</c>) and reports
    /// two mistakes at compile time: a singleton whose constructor takes a scoped service (MAXEN005), and a
    /// constructor that needs a service nobody registered (MAXEN006). Only chains that spell things out in a single
    /// expression are understood; anything cleverer is left to runtime validation, and the missing-registration check
    /// switches itself off as soon as the code registers services in a way we cannot follow (scanning, modules, ...).
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BuilderCallAnalyzer : DiagnosticAnalyzer
    {
        // Calls that put services into the container in ways we can't list. Seeing one means "we don't know what is registered".
        private static readonly HashSet<string> OpaqueCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "Scan", "RegisterModule", "RegisterModules", "RegisterGeneratedServices", "RegisterAttributed",
            "AddSource", "UseMaxenDI", "BuildMaxenServiceProvider", "DefineProfile", "UseProfile", "UseProfileFromEnvironment",
        };

        // Generic calls whose first type argument is a service they register.
        private static readonly HashSet<string> ServiceRegisteringCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "RegisterFactory", "RegisterInstance", "Replace", "ReplaceFactory", "ReplaceInstance", "Override", "AlsoAs",
        };

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(
                DiagnosticDescriptors.CaptiveDependencyInCode,
                DiagnosticDescriptors.MissingRegistration,
                DiagnosticDescriptors.TransientDisposableFromRoot,
                DiagnosticDescriptors.DeferredScopedInSingleton);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(start =>
            {
                // Registrations are gathered across the whole compilation; the verdict needs all of them.
                var facts = new RegistrationFacts();
                start.RegisterOperationAction(operation => Collect(operation, facts), OperationKind.Invocation);
                start.RegisterSymbolAction(symbol => CollectAttributed(symbol, facts), SymbolKind.NamedType);
                start.RegisterCompilationEndAction(end => Report(end, facts));
            });
        }

        // Classes marked [Service] are registered by the generator; for our purposes they are services that exist.
        private static void CollectAttributed(SymbolAnalysisContext context, RegistrationFacts facts)
        {
            var type = (INamedTypeSymbol)context.Symbol;
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.Name != "ServiceAttribute") continue;

                facts.OtherServices.Add(type);
                foreach (var argument in attribute.NamedArguments)
                    if (argument.Key == "ServiceType" && argument.Value.Value is ITypeSymbol serviceType)
                        facts.OtherServices.Add(serviceType);
            }
        }

        private static void Collect(OperationAnalysisContext context, RegistrationFacts facts)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (!method.ContainingNamespace.ToDisplayString().StartsWith("Maxen.DI", StringComparison.Ordinal)) return;

            if (OpaqueCalls.Contains(method.Name)) { facts.MarkOpaque(); return; }

            if (ResolveCalls.Contains(method.Name) && method.TypeArguments.Length == 1 && IsOnRootContainer(invocation))
            {
                facts.RootResolves.Add(new RootResolve(method.TypeArguments[0], invocation.Syntax.GetLocation()));
                return;
            }

            if (ServiceRegisteringCalls.Contains(method.Name) && method.TypeArguments.Length >= 1)
            {
                facts.OtherServices.Add(method.TypeArguments[0]);
                return;
            }

            if (method.Name != "Register") return;

            // Register(typeof(A), typeof(B)) and friends: the types are values, not type arguments, so we can't read them.
            if (method.TypeArguments.Length < 1 || method.TypeArguments.Length > 2) { facts.MarkOpaque(); return; }
            if (!(method.TypeArguments[0] is INamedTypeSymbol service)) { facts.MarkOpaque(); return; }
            if (!(method.TypeArguments[method.TypeArguments.Length - 1] is INamedTypeSymbol implementation)) { facts.MarkOpaque(); return; }

            // Follow the fluent calls chained onto Register(...) until we know the lifetime.
            string? lifetime = null;
            var specific = false;
            IOperation current = invocation;

            while (TryNextInChain(current, out var next))
            {
                switch (next.TargetMethod.Name)
                {
                    case "AsSingleton": lifetime = "Singleton"; break;
                    case "AsScoped": lifetime = "Scoped"; break;
                    case "AsTransient": lifetime = "Transient"; break;
                    case "WithKey":
                    case "When":
                    case "WhenInjectedInto":
                        specific = true;
                        break;
                    case "AlsoAs":
                        if (next.TargetMethod.TypeArguments.Length == 1) facts.OtherServices.Add(next.TargetMethod.TypeArguments[0]);
                        break;
                    case "WithLifetime":
                    case "AsScopedTo":
                    case "AsPooled":
                    case "AsPerThread":
                        // A custom lifetime: nothing sensible to say about it for the lifetime check,
                        // but the service is still registered.
                        facts.OtherServices.Add(service);
                        return;
                }
                current = next;
            }

            // Registering without a lifetime call means transient, which never captures anything.
            facts.Registrations.Add(new CodeRegistration(service, implementation, lifetime ?? "Transient", specific, invocation.Syntax.GetLocation()));
        }

        // Resolve calls whose result the receiver (and so the lifetime of a transient) is tied to.
        private static readonly HashSet<string> ResolveCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "Resolve", "TryResolve", "ResolveAsync",
        };

        // True when the thing being asked is statically the root container, so transients land in its store.
        private static bool IsOnRootContainer(IInvocationOperation invocation)
        {
            var receiver = invocation.Instance ?? invocation.Arguments.FirstOrDefault()?.Value;
            while (receiver is IConversionOperation conversion) receiver = conversion.Operand;

            var display = receiver?.Type?.ToDisplayString();
            return display == "Maxen.DI.Container" || display == "Maxen.DI.IContainer";
        }

        // The next call in "a.B().C()": either an instance call on the previous result, or an extension method receiving it.
        private static bool TryNextInChain(IOperation current, out IInvocationOperation next)
        {
            next = null!;
            if (!(current.Parent is IInvocationOperation candidate)) return false;

            var receiver = candidate.Instance ?? candidate.Arguments.FirstOrDefault()?.Value;
            while (receiver is IConversionOperation conversion) receiver = conversion.Operand;

            if (!ReferenceEquals(receiver, current)) return false;
            next = candidate;
            return true;
        }

        private static void Report(CompilationAnalysisContext context, RegistrationFacts facts)
        {
            // Source order, so "the last registration wins" means what it means at runtime.
            var ordered = facts.Registrations
                .OrderBy(r => r.Location.SourceTree?.FilePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(r => r.Location.SourceSpan.Start)
                .ToList();

            ReportCaptive(context, ordered);
            ReportRootResolves(context, ordered, facts);

            if (!facts.HasOpaqueRegistrations)
                ReportMissing(context, ordered, facts);
        }

        private static void ReportCaptive(CompilationAnalysisContext context, List<CodeRegistration> ordered)
        {
            var winners = new Dictionary<ISymbol, CodeRegistration>(SymbolEqualityComparer.Default);
            foreach (var registration in ordered.Where(r => !r.IsSpecific))
                winners[registration.Service] = registration;

            foreach (var singleton in ordered.Where(r => r.Lifetime == "Singleton" && !r.IsSpecific))
            {
                var constructor = ChooseConstructor(singleton.Implementation);
                if (constructor == null) continue;

                foreach (var parameter in constructor.Parameters)
                {
                    // A keyed parameter asks for a specific registration we deliberately did not track.
                    if (HasAttribute(parameter, "FromKeyAttribute")) continue;
                    if (!(parameter.Type is INamedTypeSymbol parameterType)) continue;

                    // Lazy<T> and Func<..., T> look harmless but remember the scope they were made in.
                    var deferred = DeferredTarget(parameterType);
                    if (deferred != null && winners.TryGetValue(deferred, out var deferredDependency) && deferredDependency.Lifetime == "Scoped")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.DeferredScopedInSingleton,
                            singleton.Location,
                            singleton.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            parameterType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            deferred.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                        continue;
                    }

                    if (winners.TryGetValue(parameterType, out var dependency) && dependency.Lifetime == "Scoped")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.CaptiveDependencyInCode,
                            singleton.Location,
                            singleton.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            parameterType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                    }
                }
            }
        }

        private static void ReportRootResolves(CompilationAnalysisContext context, List<CodeRegistration> ordered, RegistrationFacts facts)
        {
            var winners = new Dictionary<ISymbol, CodeRegistration>(SymbolEqualityComparer.Default);
            foreach (var registration in ordered.Where(r => !r.IsSpecific))
                winners[registration.Service] = registration;

            foreach (var resolve in facts.RootResolves)
            {
                if (!winners.TryGetValue(resolve.Service, out var registration) || registration.Lifetime != "Transient") continue;
                if (!IsDisposable(registration.Implementation)) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TransientDisposableFromRoot,
                    resolve.Location,
                    registration.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }

        private static bool IsDisposable(INamedTypeSymbol type)
            => type.AllInterfaces.Any(i =>
            {
                var name = i.ToDisplayString();
                return name == "System.IDisposable" || name == "System.IAsyncDisposable";
            });

        // The service a Lazy<T> or Func<..., T> will resolve later, or null for any other type.
        private static INamedTypeSymbol? DeferredTarget(INamedTypeSymbol type)
        {
            if (!type.IsGenericType || type.TypeArguments.Length == 0) return null;

            var display = type.OriginalDefinition.ToDisplayString();
            var isDeferred = display == "System.Lazy<T>" || display.StartsWith("System.Func<", StringComparison.Ordinal);
            return isDeferred ? type.TypeArguments[type.TypeArguments.Length - 1] as INamedTypeSymbol : null;
        }

        private static void ReportMissing(CompilationAnalysisContext context, List<CodeRegistration> ordered, RegistrationFacts facts)
        {
            var known = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var registration in ordered) known.Add(registration.Service);
            foreach (var service in facts.OtherServices) known.Add(service);

            foreach (var registration in ordered)
            {
                // With several constructors the container picks whichever it can satisfy, so a "missing" one proves nothing.
                var constructors = registration.Implementation.InstanceConstructors
                    .Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
                var marked = constructors.FirstOrDefault(c => HasAttribute(c, "InjectAttribute"));
                var constructor = marked ?? (constructors.Count == 1 ? constructors[0] : null);
                if (constructor == null) continue;

                foreach (var parameter in constructor.Parameters)
                {
                    if (!NeedsRegistration(parameter) || known.Contains(parameter.Type)) continue;

                    // The quick fix needs to know what to register, and whether that is a class it can register as itself.
                    var properties = ImmutableDictionary<string, string?>.Empty
                        .Add("MissingType", parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                        .Add("Concrete", (parameter.Type.TypeKind == TypeKind.Class && !parameter.Type.IsAbstract) ? "true" : "false");

                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MissingRegistration,
                        registration.Location,
                        additionalLocations: null,
                        properties: properties,
                        registration.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }
        }

        // Only parameters we are sure must be registered: plain classes and interfaces without a default, a key or a "?".
        private static bool NeedsRegistration(IParameterSymbol parameter)
        {
            if (parameter.HasExplicitDefaultValue || HasAttribute(parameter, "FromKeyAttribute")) return false;
            if (parameter.NullableAnnotation == NullableAnnotation.Annotated) return false;
            if (parameter.RefKind != RefKind.None) return false;

            var type = parameter.Type;
            if (!(type is INamedTypeSymbol named)) return false;
            if (named.TypeKind != TypeKind.Class && named.TypeKind != TypeKind.Interface) return false;
            if (named.SpecialType == SpecialType.System_String) return false;

            // The container supplies these itself, or builds them from any registration of the inner type.
            var display = named.OriginalDefinition.ToDisplayString();
            switch (display)
            {
                case "Maxen.DI.IScope":
                case "Maxen.DI.IContainer":
                case "System.IServiceProvider":
                case "System.Collections.Generic.IEnumerable<T>":
                case "System.Collections.Generic.IReadOnlyCollection<T>":
                case "System.Collections.Generic.IReadOnlyList<T>":
                case "System.Collections.Generic.ICollection<T>":
                case "System.Collections.Generic.IList<T>":
                case "System.Lazy<T>":
                case "Maxen.DI.Options.IOptions<T>":
                    return false;
            }

            // Func<...> in all its shapes.
            if (named.TypeKind == TypeKind.Delegate || display.StartsWith("System.Func<", StringComparison.Ordinal)) return false;

            return true;
        }

        private static bool HasAttribute(ISymbol symbol, string attributeName)
            => symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);

        // The same rule the runtime applies: the [Inject] constructor, otherwise the public one with the most parameters.
        private static IMethodSymbol? ChooseConstructor(INamedTypeSymbol type)
        {
            var constructors = type.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();

            var marked = constructors.FirstOrDefault(c => HasAttribute(c, "InjectAttribute"));
            if (marked != null) return marked;

            return constructors.OrderByDescending(c => c.Parameters.Length).FirstOrDefault();
        }
    }
}
