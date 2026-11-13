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
    /// For "disposable transient resolved from the root container" (MAXEN007): resolves from a scope instead, by adding
    /// <c>using var scope = container.CreateScope();</c> above the statement and calling <c>Resolve</c> on the scope.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RootResolveCodeFix)), Shared]
    public sealed class RootResolveCodeFix : CodeFixProvider
    {
        private const string TransientDisposableFromRoot = "MAXEN007";

        // A scoped service resolved from the root (MAXEN010) is cured the same way: resolve it from a scope.
        private const string ScopedFromRoot = "MAXEN010";

        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(TransientDisposableFromRoot, ScopedFromRoot);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider() => null!;

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics[0];
            var invocation = root.FindNode(diagnostic.Location.SourceSpan).AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            if (invocation == null || !(invocation.Expression is MemberAccessExpressionSyntax member)) return;

            // Only where a statement can be added in front: a plain statement inside a block.
            var statement = invocation.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault();
            if (statement == null || !(statement.Parent is BlockSyntax)) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Resolve from a new scope",
                    token => Rewrite(context.Document, statement, member.Expression, token),
                    equivalenceKey: TransientDisposableFromRoot),
                diagnostic);
        }

        private static async Task<Document> Rewrite(
            Document document, StatementSyntax statement, ExpressionSyntax receiver, CancellationToken token)
        {
            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
            if (root == null) return document;

            // Avoid a name the surrounding code already uses.
            var member = statement.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
            var used = member == null
                ? new System.Collections.Generic.HashSet<string>()
                : new System.Collections.Generic.HashSet<string>(member.DescendantTokens().Select(t => t.ValueText));
            var name = "scope";
            for (var i = 2; used.Contains(name); i++) name = "scope" + i;

            var declaration = SyntaxFactory.ParseStatement("using var " + name + " = " + receiver + ".CreateScope();")
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            var rewritten = statement.ReplaceNode(receiver, SyntaxFactory.IdentifierName(name).WithTriviaFrom(receiver));

            return document.WithSyntaxRoot(root.ReplaceNode(statement, new SyntaxNode[] { declaration, rewritten }));
        }
    }
}
