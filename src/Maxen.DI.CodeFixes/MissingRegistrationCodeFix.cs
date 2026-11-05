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
    /// For "constructor needs a service that is not registered" (MAXEN006): adds <c>builder.Register&lt;Dependency&gt;();</c>
    /// above the registration that needs it. Only offered when the missing service is a concrete class, because for an
    /// interface the fix cannot know which implementation you mean.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingRegistrationCodeFix)), Shared]
    public sealed class MissingRegistrationCodeFix : CodeFixProvider
    {
        private const string MissingRegistration = "MAXEN006";

        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(MissingRegistration);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics[0];
            if (!diagnostic.Properties.TryGetValue("MissingType", out var missing) || string.IsNullOrEmpty(missing)) return;
            if (!diagnostic.Properties.TryGetValue("Concrete", out var concrete) || concrete != "true") return;

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var statement = root.FindNode(diagnostic.Location.SourceSpan).AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
            var builder = statement == null ? null : BuilderExpression(statement);
            if (statement == null || builder == null) return;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Register " + missing,
                    token => Insert(context.Document, statement, builder, missing!, token),
                    equivalenceKey: MissingRegistration + missing),
                diagnostic);
        }

        // "builder.Register<A>().AsScoped();": walk down the chain to the Register call and take what it is called on.
        private static ExpressionSyntax? BuilderExpression(ExpressionStatementSyntax statement)
        {
            ExpressionSyntax current = statement.Expression;
            while (current is InvocationExpressionSyntax invocation && invocation.Expression is MemberAccessExpressionSyntax member)
            {
                if (member.Name.Identifier.ValueText == "Register") return member.Expression;
                current = member.Expression;
            }
            return null;
        }

        private static async Task<Document> Insert(
            Document document, ExpressionStatementSyntax statement, ExpressionSyntax builder, string typeName, CancellationToken token)
        {
            var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
            if (root == null) return document;

            var added = SyntaxFactory.ParseStatement(builder.ToString() + ".Register<" + typeName + ">();")
                .WithLeadingTrivia(statement.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            return document.WithSyntaxRoot(root.InsertNodesBefore(statement, new[] { added }));
        }
    }
}
