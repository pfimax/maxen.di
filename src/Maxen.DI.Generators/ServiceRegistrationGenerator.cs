using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// Finds every class marked with Maxen.DI's [Service] attribute and generates a
    /// <c>RegisterGeneratedServices</c> extension method that registers them with plain
    /// <c>new Foo(...)</c> factories: no reflection at startup, and friendly to trimming and AOT.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class ServiceRegistrationGenerator : IIncrementalGenerator
    {
        private const string ServiceAttributeName = "Maxen.DI.ServiceAttribute";
        private const string DecoratorAttributeName = "Maxen.DI.DecoratorAttribute";
        private const string InterceptAttributeName = "Maxen.DI.InterceptAttribute";

        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var services = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ServiceAttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (syntaxContext, token) => ServiceModelFactory.Create(syntaxContext, token))
                .Collect();

            // Decorators travel through the same generated method, after the services they wrap.
            var decorators = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    DecoratorAttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (syntaxContext, token) => DecoratorModelFactory.Create(syntaxContext, token))
                .Collect();

            var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName ?? "Assembly");

            context.RegisterSourceOutput(
                services.Combine(decorators).Combine(assemblyName),
                static (productionContext, pair) => SourceEmitter.Emit(
                    productionContext, pair.Left.Left.AddRange(pair.Left.Right), pair.Right));

            // Second job: remember how to call the constructors of everything registered in code, so the
            // container does not have to compile expression trees for them at runtime.
            var activators = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => ActivatorModelFactory.IsCandidate(node),
                    static (syntaxContext, token) => ActivatorModelFactory.Create(syntaxContext, token))
                .Where(static model => model != null)
                .Collect();

            context.RegisterSourceOutput(
                activators.Combine(assemblyName),
                static (productionContext, pair) => ActivatorEmitter.Emit(productionContext, pair.Left, pair.Right));

            // Third job: a proxy class for every interface you intercept in code, so interception needs no runtime code generation.
            var proxies = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => ProxyModelFactory.IsCandidate(node),
                    static (syntaxContext, token) => ProxyModelFactory.Create(syntaxContext, token))
                .Where(static model => model != null)
                .Collect();

            // Interfaces named by [Intercept] on a [Service] class need a proxy too.
            var attributeProxies = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    InterceptAttributeName,
                    static (node, _) => node is ClassDeclarationSyntax,
                    static (syntaxContext, token) => AttributeProxyModelFactory.Create(syntaxContext, token))
                .Where(static model => model != null)
                .Collect();

            context.RegisterSourceOutput(
                proxies.Combine(attributeProxies).Combine(assemblyName),
                static (productionContext, pair) => ProxyEmitter.Emit(
                    productionContext, pair.Left.Left.AddRange(pair.Left.Right), pair.Right));
        }
    }
}
