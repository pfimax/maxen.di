using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    internal static class DiagnosticDescriptors
    {
        private const string Category = "Maxen.DI";

        public static readonly DiagnosticDescriptor AmbiguousConstructor = new DiagnosticDescriptor(
            id: "MAXEN001",
            title: "Ambiguous constructor",
            messageFormat: "'{0}' has several constructors with the same number of parameters; mark the one to use with [Inject]. The class was not registered by the generator.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedClass = new DiagnosticDescriptor(
            id: "MAXEN002",
            title: "Class not supported by the generator",
            messageFormat: "'{0}' cannot be registered by the generator ({1}). Use RegisterAttributed at runtime for it instead.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CaptiveDependency = new DiagnosticDescriptor(
            id: "MAXEN004",
            title: "Singleton depends on a scoped service",
            messageFormat: "Singleton '{0}' takes the scoped service '{1}' in its constructor, so that instance would outlive its scope. Make '{0}' scoped or transient, or '{1}' a singleton.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CaptiveDependencyInCode = new DiagnosticDescriptor(
            id: "MAXEN005",
            title: "Singleton registration depends on a scoped registration",
            messageFormat: "Singleton '{0}' takes the scoped service '{1}' in its constructor, so that instance would outlive its scope. Register '{0}' as scoped or transient, or '{1}' as a singleton.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor MissingRegistration = new DiagnosticDescriptor(
            id: "MAXEN006",
            title: "Constructor needs a service that is not registered",
            messageFormat: "'{0}' takes '{1}' in its constructor, but nothing in this project registers '{1}'. Register it, give the parameter a default value, or mark it [FromKey].",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor TransientDisposableFromRoot = new DiagnosticDescriptor(
            id: "MAXEN007",
            title: "Disposable transient resolved from the root container",
            messageFormat: "'{0}' is a disposable transient, and resolving it from the root container keeps every instance alive until the container itself is disposed. Resolve it from a scope instead.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor DeferredScopedInSingleton = new DiagnosticDescriptor(
            id: "MAXEN009",
            title: "Lazy or Func of a scoped service inside a singleton",
            messageFormat: "Singleton '{0}' takes '{1}' of the scoped service '{2}'. The delegate keeps the scope it was created in, so the scoped service can be resolved after that scope has ended. Take IContainer and create a scope per use instead.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

        public static readonly DiagnosticDescriptor DecoratorWithoutInner = new DiagnosticDescriptor(
            id: "MAXEN008",
            title: "Decorator does not take the service it wraps",
            messageFormat: "'{0}' is a decorator of '{1}' but no constructor takes a '{1}' parameter, so it has nothing to wrap",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NotAssignable = new DiagnosticDescriptor(
            id: "MAXEN003",
            title: "Class does not implement its service type",
            messageFormat: "'{0}' does not implement '{1}', the ServiceType named in its [Service] attribute",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
