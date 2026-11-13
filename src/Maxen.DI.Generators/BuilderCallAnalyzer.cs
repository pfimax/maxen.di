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
    /// mistakes at compile time: a singleton whose constructor takes a scoped service (MAXEN005), and a
    /// constructor that needs a service nobody registered (MAXEN006). Only chains that spell things out in a single
    /// expression are understood; anything cleverer is left to runtime validation, and the missing-registration check
    /// switches itself off as soon as the code registers services in a way we cannot follow (scanning, modules, ...).
    /// </summary>
    /// <remarks>
    /// A project can build more than one container. A builder that is created in a local variable and only ever has
    /// calls made on it is a container the analyzer can see all of, so it is judged by its own registrations. Every other
    /// registration (made on a parameter, a field, a builder that was handed to another method) could belong to any
    /// container, so those are judged together against everything the project registers, which errs on the quiet side.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class BuilderCallAnalyzer : DiagnosticAnalyzer
    {
        // Calls that put services into the container in ways we can't list. Seeing one means "we don't know what is registered".
        // RegisterGeneratedServices is not one of them: it registers exactly the [Service] classes of this compilation, and
        // CollectAttributed already counts those.
        private static readonly HashSet<string> OpaqueCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "Scan", "RegisterAssemblyTypes", "RegisterModule", "RegisterModules", "RegisterAttributed", "Populate",
            "AddSource", "UseMaxenDI", "BuildMaxenServiceProvider", "DefineProfile", "UseProfile", "UseProfileFromEnvironment",
        };

        // Generic calls whose first type argument is a service they register.
        private static readonly HashSet<string> ServiceRegisteringCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "RegisterFactory", "RegisterInstance", "TryRegister", "Replace", "ReplaceFactory", "ReplaceInstance", "Override", "AlsoAs",
        };

        // The same calls take the service as a Type value too (RegisterFactory(typeof(IFoo), ...)), and then it can't be read.
        private static readonly HashSet<string> ServiceAsValueCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "RegisterFactory", "RegisterInstance", "AlsoAs",
        };

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(
                DiagnosticDescriptors.CaptiveDependencyInCode,
                DiagnosticDescriptors.MissingRegistration,
                DiagnosticDescriptors.TransientDisposableFromRoot,
                DiagnosticDescriptors.DeferredScopedInSingleton,
                DiagnosticDescriptors.ScopedFromRoot);

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
                start.RegisterOperationAction(operation => CollectDeclared(operation, facts), OperationKind.VariableDeclarator);
                start.RegisterOperationAction(operation => CollectUse(operation, facts), OperationKind.LocalReference);
                start.RegisterOperationAction(operation => CollectOption(operation, facts), OperationKind.SimpleAssignment);
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

                facts.AttributedServices.Add(type);
                foreach (var argument in attribute.NamedArguments)
                    if (argument.Key == "ServiceType" && argument.Value.Value is ITypeSymbol serviceType)
                        facts.AttributedServices.Add(serviceType);
            }
        }

        // "var builder = new ContainerBuilder();": a builder we watched being created, so we know it starts empty.
        private static void CollectDeclared(OperationAnalysisContext context, RegistrationFacts facts)
        {
            var declarator = (IVariableDeclaratorOperation)context.Operation;
            if (!IsBuilder(declarator.Symbol.Type)) return;

            if (Unwrap(declarator.GetVariableInitializer()?.Value) is IObjectCreationOperation)
                facts.MarkCreated(declarator.Symbol);
        }

        // "builder.Options.ValidateScopes = false": from here on a scoped service may be resolved from the root.
        private static void CollectOption(OperationAnalysisContext context, RegistrationFacts facts)
        {
            var assignment = (ISimpleAssignmentOperation)context.Operation;
            if (!(assignment.Target is IPropertyReferenceOperation property)) return;
            if (property.Property.Name != "ValidateScopes" || property.Property.ContainingType?.Name != "ContainerOptions") return;

            // Anything but a plain "true" might be false at run time.
            var value = assignment.Value.ConstantValue;
            if (!value.HasValue || !(value.Value is bool on) || !on) facts.MarkScopeValidationOff();
        }

        // Every mention of a builder variable. The harmless ones are calls made on it and reads of its properties;
        // anything else (an argument to some method, an assignment, a return) means other code gets hold of it.
        private static void CollectUse(OperationAnalysisContext context, RegistrationFacts facts)
        {
            var reference = (ILocalReferenceOperation)context.Operation;
            if (!IsBuilder(reference.Local.Type)) return;

            IOperation use = reference;
            while (use.Parent is IConversionOperation || use.Parent is IParenthesizedOperation) use = use.Parent;

            var harmless = false;
            switch (use.Parent)
            {
                case IInvocationOperation call:
                    harmless = ReferenceEquals(call.Instance, use) && IsMaxen(call.TargetMethod);
                    break;
                case IArgumentOperation argument when argument.Parent is IInvocationOperation call:
                    // builder.Register<T>() is an extension method, so the builder arrives as its first argument.
                    harmless = call.TargetMethod.IsExtensionMethod && argument.Parameter?.Ordinal == 0 && IsMaxen(call.TargetMethod)
                        && !IsModuleConfigure(call.TargetMethod);
                    break;
                case IMemberReferenceOperation member:
                    harmless = ReferenceEquals(member.Instance, use);
                    break;
            }

            if (!harmless) facts.MarkHandedAround(reference.Local);
        }

        private static void Collect(OperationAnalysisContext context, RegistrationFacts facts)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (!IsMaxen(method))
            {
                CollectForeign(invocation, facts);
                return;
            }

            var builder = BuilderOf(invocation);

            if (OpaqueCalls.Contains(method.Name) || IsModuleConfigure(method)) { facts.MarkOpaque(builder); return; }

            if (ResolveCalls.Contains(method.Name) && method.TypeArguments.Length == 1 && IsOnRootContainer(invocation))
            {
                facts.RootResolves.Add(new RootResolve(method.TypeArguments[0], invocation.Syntax.GetLocation()));
                return;
            }

            if (ServiceRegisteringCalls.Contains(method.Name) && method.TypeArguments.Length >= 1)
            {
                facts.OtherServices.Add(new KeyValuePair<ITypeSymbol, ILocalSymbol?>(method.TypeArguments[0], builder));
                return;
            }

            // The service is a Type value or sits inside a descriptor: registered, but we can't say what.
            if (method.TypeArguments.Length == 0
                && (ServiceAsValueCalls.Contains(method.Name) || (method.Name == "Add" && method.ContainingType?.Name == "ContainerBuilder")))
            {
                facts.MarkOpaque(builder);
                return;
            }

            if (method.Name != "Register") return;

            // Register(typeof(A), typeof(B)) and friends: the types are values, not type arguments, so we can't read them.
            if (method.TypeArguments.Length < 1 || method.TypeArguments.Length > 2) { facts.MarkOpaque(builder); return; }
            if (!(method.TypeArguments[0] is INamedTypeSymbol service)) { facts.MarkOpaque(builder); return; }
            if (!(method.TypeArguments[method.TypeArguments.Length - 1] is INamedTypeSymbol implementation)) { facts.MarkOpaque(builder); return; }

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
                    case "WithLifetime":
                    case "AsScopedTo":
                    case "AsPooled":
                    case "AsPerThread":
                        // A custom lifetime: nothing sensible to say about it for the lifetime check,
                        // but the service is still registered.
                        facts.OtherServices.Add(new KeyValuePair<ITypeSymbol, ILocalSymbol?>(service, builder));
                        return;
                }
                current = next;
            }

            // Registering without a lifetime call means transient, which never captures anything.
            facts.Registrations.Add(new CodeRegistration(service, implementation, lifetime ?? "Transient", specific, invocation.Syntax.GetLocation(), builder));
        }

        // A call to a method that isn't ours. If it is handed a builder and its body is in another assembly, it may
        // register anything: an "AddDataServices(builder)" helper from a referenced project, say. A method written in
        // this project needs no such caution, because its body is read like any other code.
        private static void CollectForeign(IInvocationOperation invocation, RegistrationFacts facts)
        {
            var method = invocation.TargetMethod;
            if (method.MethodKind == MethodKind.DelegateInvoke) return;
            if (!(method.ReducedFrom ?? method).OriginalDefinition.DeclaringSyntaxReferences.IsEmpty) return;

            foreach (var argument in invocation.Arguments)
            {
                if (argument.Parameter != null && IsBuilder(argument.Parameter.Type))
                {
                    facts.MarkOpaque();
                    return;
                }
            }
        }

        // Resolve calls whose result the receiver (and so the lifetime of a transient) is tied to.
        private static readonly HashSet<string> ResolveCalls = new HashSet<string>(StringComparer.Ordinal)
        {
            "Resolve", "TryResolve", "ResolveAsync",
        };

        private static bool IsMaxen(IMethodSymbol method)
            => method.ContainingNamespace.ToDisplayString().StartsWith("Maxen.DI", StringComparison.Ordinal);

        private static bool IsBuilder(ITypeSymbol? type)
            => type != null && type.Name == "ContainerBuilder" && type.ContainingNamespace?.ToDisplayString() == "Maxen.DI";

        // module.Configure(builder), called by hand instead of through RegisterModule. Options' Configure<T> is generic.
        private static bool IsModuleConfigure(IMethodSymbol method)
            => method.Name == "Configure" && method.ContainingType?.Name == "IContainerModule";

        private static IOperation? Unwrap(IOperation? operation)
        {
            while (true)
            {
                if (operation is IConversionOperation conversion) operation = conversion.Operand;
                else if (operation is IParenthesizedOperation parenthesized) operation = parenthesized.Operand;
                else return operation;
            }
        }

        // What a call is made on: the instance, or the first argument of an extension method.
        private static IOperation? ReceiverOf(IInvocationOperation invocation)
            => Unwrap(invocation.Instance ?? invocation.Arguments.FirstOrDefault()?.Value);

        // The builder variable at the start of "builder.Use(...).Register<A>().AsSingleton()", or null when the chain
        // starts with anything but a local variable.
        private static ILocalSymbol? BuilderOf(IInvocationOperation invocation)
        {
            var receiver = ReceiverOf(invocation);
            while (receiver is IInvocationOperation previous && IsMaxen(previous.TargetMethod))
                receiver = ReceiverOf(previous);

            return receiver is ILocalReferenceOperation local && IsBuilder(local.Local.Type) ? local.Local : null;
        }

        // True when the thing being asked is statically the root container, so transients land in its store.
        private static bool IsOnRootContainer(IInvocationOperation invocation)
        {
            var display = ReceiverOf(invocation)?.Type?.ToDisplayString();
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

            ReportRootResolves(context, ordered, facts);

            // Registrations we can't tie to one container are checked against everything the project registers.
            var shared = ordered.Where(r => !facts.IsOwnContainer(r.Builder)).ToList();
            ReportCaptive(context, shared, ordered);
            if (!facts.HasOpaqueRegistrations)
            {
                var known = Known(ordered, facts.OtherServices.Select(s => s.Key), facts);
                ReportMissing(context, shared, known);
            }

            // A builder we can see all of is a container of its own: only its registrations count for it.
            var own = ordered.Where(r => facts.IsOwnContainer(r.Builder))
                .GroupBy<CodeRegistration, ISymbol>(r => r.Builder!, SymbolEqualityComparer.Default);
            foreach (var container in own)
            {
                var builder = (ILocalSymbol)container.Key;
                var registrations = container.ToList();
                ReportCaptive(context, registrations, registrations);

                if (facts.IsOpaque(builder)) continue;

                var others = facts.OtherServices
                    .Where(s => s.Value != null && SymbolEqualityComparer.Default.Equals(s.Value, builder))
                    .Select(s => s.Key);
                ReportMissing(context, registrations, Known(registrations, others, facts));
            }
        }

        // Every service type that counts as registered: the given registrations, the given other calls, and [Service] classes.
        private static HashSet<ISymbol> Known(IEnumerable<CodeRegistration> registrations, IEnumerable<ITypeSymbol> others, RegistrationFacts facts)
        {
            var known = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var registration in registrations) known.Add(registration.Service);
            foreach (var service in others) known.Add(service);
            foreach (var service in facts.AttributedServices) known.Add(service);
            return known;
        }

        // Checks the singletons among "subjects" against the registrations they could be given, "visible".
        private static void ReportCaptive(CompilationAnalysisContext context, List<CodeRegistration> subjects, List<CodeRegistration> visible)
        {
            var winners = new Dictionary<ISymbol, CodeRegistration>(SymbolEqualityComparer.Default);
            foreach (var registration in visible.Where(r => !r.IsSpecific))
                winners[registration.Service] = registration;

            foreach (var singleton in subjects.Where(r => r.Lifetime == "Singleton" && !r.IsSpecific))
            {
                var constructor = ChooseConstructor(singleton.Implementation);
                if (constructor == null) continue;

                foreach (var parameter in constructor.Parameters)
                {
                    // A keyed parameter asks for a specific registration we deliberately did not track.
                    if (IsKeyed(parameter)) continue;
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

                    if (!winners.TryGetValue(parameterType, out var dependency)) continue;

                    if (dependency.Lifetime == "Scoped")
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.CaptiveDependencyInCode,
                            singleton.Location,
                            singleton.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            parameterType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            "in its constructor"));
                        continue;
                    }

                    // A transient in between hides nothing: it is built for the singleton and stays with it, and so
                    // does whatever scoped service the transient takes.
                    var through = new List<string>();
                    if (dependency.Lifetime == "Transient"
                        && TryFindScopedBelow(dependency, winners, new HashSet<ISymbol>(SymbolEqualityComparer.Default), through, out var scoped))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.CaptiveDependencyInCode,
                            singleton.Location,
                            singleton.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            scoped.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            (through.Count == 1 ? "through the transient '" : "through the transients '") + string.Join("' -> '", through) + "'"));
                    }
                }
            }
        }

        // Follows a transient's constructor down to the first scoped service, through further transients if need be.
        // "through" ends up holding the transients on the way, outermost first.
        private static bool TryFindScopedBelow(
            CodeRegistration transient, Dictionary<ISymbol, CodeRegistration> winners, HashSet<ISymbol> seen, List<string> through,
            out INamedTypeSymbol scoped)
        {
            scoped = null!;
            if (!seen.Add(transient.Implementation)) return false;

            // Only when there is no doubt which constructor runs: with several, the container picks at run time.
            var constructor = OnlyConstructor(transient.Implementation);
            if (constructor == null) return false;

            through.Add(transient.Implementation.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            foreach (var parameter in constructor.Parameters)
            {
                if (IsKeyed(parameter) || !(parameter.Type is INamedTypeSymbol parameterType)) continue;
                if (!winners.TryGetValue(parameterType, out var dependency)) continue;

                if (dependency.Lifetime == "Scoped")
                {
                    scoped = parameterType;
                    return true;
                }

                if (dependency.Lifetime == "Transient" && TryFindScopedBelow(dependency, winners, seen, through, out scoped))
                    return true;
            }

            through.RemoveAt(through.Count - 1);
            return false;
        }

        // The [Inject] constructor, or the only public one; null when the choice is made at run time.
        private static IMethodSymbol? OnlyConstructor(INamedTypeSymbol type)
        {
            var constructors = type.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
            return constructors.FirstOrDefault(c => HasAttribute(c, "InjectAttribute")) ?? (constructors.Count == 1 ? constructors[0] : null);
        }

        private static void ReportRootResolves(CompilationAnalysisContext context, List<CodeRegistration> ordered, RegistrationFacts facts)
        {
            var winners = new Dictionary<ISymbol, CodeRegistration>(SymbolEqualityComparer.Default);
            foreach (var registration in ordered.Where(r => !r.IsSpecific))
                winners[registration.Service] = registration;

            foreach (var resolve in facts.RootResolves)
            {
                if (!winners.TryGetValue(resolve.Service, out var registration)) continue;

                if (registration.Lifetime == "Scoped")
                {
                    // Only an error while scope validation is on, which it is unless the code says otherwise.
                    if (!facts.ScopeValidationSwitchedOff)
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.ScopedFromRoot,
                            resolve.Location,
                            resolve.Service.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                    continue;
                }

                if (registration.Lifetime != "Transient" || !IsDisposable(registration.Implementation)) continue;

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

        // Flags the constructors among "subjects" that need a service which is not in "known".
        private static void ReportMissing(CompilationAnalysisContext context, List<CodeRegistration> subjects, HashSet<ISymbol> known)
        {
            foreach (var registration in subjects)
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
            if (parameter.HasExplicitDefaultValue || IsKeyed(parameter)) return false;
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

        // Keyed parameters ask for registrations this analyzer does not track. [ServiceKey] is not a service at all, and
        // Microsoft's attributes count too, because the container honours them.
        private static bool IsKeyed(IParameterSymbol parameter)
            => HasAttribute(parameter, "FromKeyAttribute")
                || HasAttribute(parameter, "FromKeyedServicesAttribute")
                || HasAttribute(parameter, "ServiceKeyAttribute");

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
