using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Maxen.DI.CodeFixes
{
    /// <summary>
    /// Offers the quick fix for a singleton that captures a scoped service: make the singleton scoped (or transient)
    /// instead. Works for the <c>[Service]</c> attribute (MAXEN004) and for <c>Register&lt;T&gt;().AsSingleton()</c> (MAXEN005).
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CaptiveLifetimeCodeFix)), Shared]
    public sealed class CaptiveLifetimeCodeFix : CodeFixProvider
    {
        // The ids the analyzer and generator report; written out here because those descriptors are internal to their assembly.
        private const string CaptiveInAttribute = "MAXEN004";
        private const string CaptiveInCode = "MAXEN005";

        private static readonly string[] Targets = { "Scoped", "Transient" };

        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(CaptiveInAttribute, CaptiveInCode);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics[0];
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            // The syntax that spells out "Singleton": a name in the attribute, or in the AsSingleton() call.
            var singleton = diagnostic.Id == CaptiveInAttribute
                ? FindInAttribute(node)
                : FindInChain(node);
            if (singleton == null) return;

            foreach (var target in Targets)
            {
                var replacement = SyntaxFactory.IdentifierName(
                    diagnostic.Id == CaptiveInAttribute ? target : "As" + target)
                    .WithTriviaFrom(singleton);

                context.RegisterCodeFix(
                    CodeAction.Create(
                        "Make it " + target.ToLowerInvariant(),
                        token => Replace(context.Document, singleton, replacement, token),
                        equivalenceKey: diagnostic.Id + target),
                    diagnostic);
            }
        }

        // [Service(ServiceLifetime.Singleton)] on the class the diagnostic points at.
        private static SimpleNameSyntax? FindInAttribute(SyntaxNode node)
        {
            var declaration = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (declaration == null) return null;

            return declaration.AttributeLists
                .SelectMany(list => list.Attributes)
                .Where(a => a.Name.ToString().EndsWith("Service", System.StringComparison.Ordinal)
                            || a.Name.ToString().EndsWith("ServiceAttribute", System.StringComparison.Ordinal))
                .Select(a => a.ArgumentList?.Arguments.FirstOrDefault()?.Expression)
                .OfType<MemberAccessExpressionSyntax>()
                .Select(m => m.Name)
                .FirstOrDefault(name => name.Identifier.ValueText == "Singleton");
        }

        // builder.Register<A>().AsSingleton(): start at the Register call and walk along the chain.
        private static SimpleNameSyntax? FindInChain(SyntaxNode node)
        {
            SyntaxNode? current = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();

            while (current != null && current.Parent is MemberAccessExpressionSyntax member
                   && member.Parent is InvocationExpressionSyntax outer)
            {
                if (member.Name.Identifier.ValueText == "AsSingleton") return member.Name;
                current = outer;
            }
            return null;
        }

        private static async Task<Document> Replace(
            Document document, SyntaxNode oldNode, SyntaxNode newNode, CancellationToken token)
        {
            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
            return root == null ? document : document.WithSyntaxRoot(root.ReplaceNode(oldNode, newNode));
        }
    }
}
